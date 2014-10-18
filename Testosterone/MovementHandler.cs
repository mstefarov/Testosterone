using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Testosterone {
    internal class MovementHandler {
        public readonly Player Player;


        public MovementHandler([NotNull] Player player) {
            if (player == null) throw new ArgumentNullException("player");
            Player = player;
        }


        // anti-speedhack vars
        int speedHackDetectionCounter,
            positionSyncCounter;

        const int AntiSpeedMaxJumpDelta = 25, // 16 for normal client, 25 for WoM
            AntiSpeedMaxDistanceSquared = 1024, // 32 * 32
            AntiSpeedMaxPacketCount = 200,
            AntiSpeedMaxPacketInterval = 5,
            PositionSyncInterval = 20;

        // anti-speedhack vars: packet spam
        readonly Queue<DateTime> antiSpeedPacketLog = new Queue<DateTime>();
        DateTime antiSpeedLastNotification = DateTime.UtcNow;
        public Position LastValidPosition;

        readonly Queue<Position> deltas = new Queue<Position>();
        DateTime lastSpamTime = DateTime.MinValue;
        DateTime lastMoveTime = DateTime.MinValue;
        DateTime firstMoveTime = DateTime.MinValue;


        public void ProcessMovementPacket() {
            Player.Reader.ReadByte();
            Position newPos = new Position {
                X = Player.Reader.ReadInt16(),
                Z = Player.Reader.ReadInt16(),
                Y = Player.Reader.ReadInt16(),
                R = Player.Reader.ReadByte(),
                L = Player.Reader.ReadByte()
            };
            Position oldPos = Player.Position;

            // calculate difference between old and new positions
            Position delta = new Position {
                X = (short)(newPos.X - oldPos.X),
                Y = (short)(newPos.Y - oldPos.Y),
                Z = (short)(newPos.Z - oldPos.Z),
                R = (byte)Math.Abs(newPos.R - oldPos.R),
                L = (byte)Math.Abs(newPos.L - oldPos.L)
            };

            bool posChanged = (delta.X != 0) || (delta.Y != 0) || (delta.Z != 0);
            bool rotChanged = (delta.R != 0) || (delta.L != 0);

            // delta counters!
            if (posChanged) {
                if (deltas.Count == 0) {
                    deltas.Enqueue(oldPos);
                    firstMoveTime = DateTime.UtcNow;
                }
                deltas.Enqueue(newPos);
                lastMoveTime = DateTime.UtcNow;
            } else {
                if (lastMoveTime > lastSpamTime && DateTime.UtcNow.Subtract(lastMoveTime) > TimeSpan.FromSeconds(0.5)) {
                    SpamMovementStats();
                    lastSpamTime = DateTime.UtcNow;
                }
            }

            // skip everything if player hasn't moved
            if (!posChanged && !rotChanged) return;

            // only reset the timer if player rotated
            // if player is just pushed around, rotation does not change (and timer should not reset)
            if (rotChanged) Player.ResetIdleTimer();

            if (!Player.IsOp && !Config.AllowSpeedHack || Player.IsOp && !Config.OpAllowSpeedHack) {
                int distSquared = delta.X*delta.X + delta.Y*delta.Y + delta.Z*delta.Z;
                // speedhack detection
                if (DetectMovementPacketSpam()) return;
                if ((distSquared - delta.Z*delta.Z > AntiSpeedMaxDistanceSquared ||
                     delta.Z > AntiSpeedMaxJumpDelta) && speedHackDetectionCounter >= 0) {

                    if (speedHackDetectionCounter == 0) {
                        LastValidPosition = Player.Position;
                    } else if (speedHackDetectionCounter > 1) {
                        DenyMovement();
                        speedHackDetectionCounter = 0;
                        return;
                    }
                    speedHackDetectionCounter++;

                } else {
                    speedHackDetectionCounter = 0;
                }
            }

            BroadcastMovementChange(newPos, delta);
        }


        void SpamMovementStats() {
            int minDelta = int.MaxValue,
                maxDelta = int.MinValue,
                totalDelta = 0;
            var zs = deltas.Select(pos => pos.Z).ToArray();
            int totalDisplacement = zs.Last() - zs.First();
            int minZ = zs.Min();
            int maxZ = zs.Max();
            int maxDisplacement = Math.Abs(minZ - maxZ);
            for (int i = 1; i < zs.Length; i++) {
                int deltaZ = zs[i] - zs[i - 1];
                minDelta = Math.Min(minDelta, deltaZ);
                maxDelta = Math.Max(maxDelta, deltaZ);
                totalDelta += Math.Abs(deltaZ);
            }
            Player.MessageNow("{0:HH:mm:ss} &FHeight: Min={1} Max={2} &CJumpHeight={3}",
                              DateTime.UtcNow, minZ, maxZ, maxDisplacement);
            Player.MessageNow("&FZ-Velocity: Min={0} Max={1} | Dist: {2} | Displ: {3}",
                              minDelta, maxDelta, totalDelta, totalDisplacement);
            Player.MessageNow("&FTime: {0:0} ms", Math.Round((lastMoveTime - firstMoveTime).TotalMilliseconds));
            deltas.Clear();
        }


        void BroadcastMovementChange(Position newPos, Position delta) {
            Player.Position = newPos;

            bool posChanged = (delta.X != 0) || (delta.Y != 0) || (delta.Z != 0);
            bool rotChanged = (delta.R != 0) || (delta.L != 0);

            Packet packet;
            // create the movement packet
            if (delta.FitsIntoMoveRotatePacket && positionSyncCounter < PositionSyncInterval) {
                if (posChanged && rotChanged) {
                    // incremental position + rotation update
                    packet = Packet.MakeMoveRotate(Player.Id, new Position {
                        X = delta.X,
                        Y = delta.Y,
                        Z = delta.Z,
                        R = newPos.R,
                        L = newPos.L
                    });

                } else if (posChanged) {
                    // incremental position update
                    packet = Packet.MakeMove(Player.Id, delta);

                } else if (rotChanged) {
                    // absolute rotation update
                    packet = Packet.MakeRotate(Player.Id, newPos);
                } else {
                    return;
                }

            } else {
                // full (absolute position + rotation) update
                packet = Packet.MakeTeleport(Player.Id, newPos);
            }

            positionSyncCounter++;
            if (positionSyncCounter >= PositionSyncInterval) {
                positionSyncCounter = 0;
            }

            Server.Players.Send(Player, packet);
        }


        bool DetectMovementPacketSpam() {
            if (antiSpeedPacketLog.Count >= AntiSpeedMaxPacketCount) {
                DateTime oldestTime = antiSpeedPacketLog.Dequeue();
                double spamTimer = DateTime.UtcNow.Subtract(oldestTime).TotalSeconds;
                if (spamTimer < AntiSpeedMaxPacketInterval) {
                    DenyMovement();
                    return true;
                }
            }
            antiSpeedPacketLog.Enqueue(DateTime.UtcNow);
            return false;
        }


        void DenyMovement() {
            Player.Writer.Write(Packet.MakeSelfTeleport(LastValidPosition).Bytes);
            if (DateTime.UtcNow.Subtract(antiSpeedLastNotification).Seconds > 1) {
                Player.Message("You are not allowed to speedhack.");
                antiSpeedLastNotification = DateTime.UtcNow;
            }
        }
    }
}
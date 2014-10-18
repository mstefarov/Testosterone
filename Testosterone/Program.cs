namespace Testosterone {
    public class Program {
        public static int Main(string[] args) {
            Server server = new Server(new Config("server.properties"));
            server.Start();
            return 0;
        }
    }
}
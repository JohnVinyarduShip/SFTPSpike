using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace SFTPSpike
{
    public class Program
    {
        public static void Main(string[] args)
        {
            const string usageMessage = @"
SFTPSpike

Usage:
    SFTPSpike <username> <password>

Example:
    uShip.Tracking.UI ftp_username ftp_password

Options:
    --help      Show this screen
";
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine(usageMessage);
                return;
            }

            var userName = args[0];
            var password = args[1];

            var connection = new ConnectionInfo(
                "207.207.37.67",
                22,
                userName,
                new AuthenticationMethod[]
                {
                    new PasswordAuthenticationMethod(
                        userName,
                        Encoding.UTF8.GetBytes(password)), 
                });

            using (var client = new SftpClient(connection))
            {
                client.Connect();
                foreach (var c in client.Each("/EDI/214"))
                {
                    Handle214File(client, c);
                }
            }
            Console.WriteLine("Done Processing.  Press any key...");
            Console.ReadLine();
        }

        private static void Handle214File(SftpClient client, SftpFile file)
        {
            var content = client.ReadAllText(file.FullName);
            Console.WriteLine(content);
            using (var httpClient = new HttpClient())
            {
                var body = new StringContent(
                    content, Encoding.UTF8, "application/x-12");
                Console.WriteLine(file.Name);
                var response = httpClient.PostAsync(
                    new Uri("http://devnull-as-a-service.com/dev/null"),
                    body).Result;
                Console.WriteLine(
                    "Response from devnull-as-a-service.com was {0}", 
                    response.StatusCode);
                Console.WriteLine("TODO: Move to {0}", file.ProcessedPath());
                Console.WriteLine("=========================================");
            }
        }
    }

    public static class SftpClientExtensions
    {
        public static IEnumerable<SftpFile> Each(
            this SftpClient client, string pathPart)
        {
            return client
                .Ls(client.WorkingDirectory)
                .Select(path => path.JoinPathWith(pathPart))
                .SelectMany(np => client.Ls(np)
                .Where(p => p.IsRegularFile));
        }

        public static IEnumerable<SftpFile> Ls(
            this SftpClient client, string path)
        {
            if (!client.Exists(path))
            {
                return Enumerable.Empty<SftpFile>();
            }
            return client.ListDirectory(path).Where(x =>
                x.Name != "." && x.Name != "..");
        }
    }

    public static class SftpFileExtensions
    {
        public static string ProcessedPath(this SftpFile file)
        {
            var path = file.FullName.Replace(file.Name, String.Empty);
            return KludgeyUnixStyleJoin(path, "Processed", file.Name);
        }

        public static string JoinPathWith(this SftpFile directory, string part)
        {
            return KludgeyUnixStyleJoin(directory.FullName, part);
        }

        private static string KludgeyUnixStyleJoin(params string[] s)
        {
            var o = String.Join("/", s);
            return Regex.Replace(o, "/+", "/");
        }
    }


}

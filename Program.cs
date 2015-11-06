using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Net.Security;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace testimap
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			TcpListener listener = new TcpListener (8143);

			listener.Start ();

			var log = NLog.LogManager.GetCurrentClassLogger ();

			log.Info ("Listener started on 8143");

			Task.Run (async () => {

				while (true) {
					var incoming = await listener.AcceptTcpClientAsync ();

					var client = new TcpClient ();
//					client.Client.Blocking = false;
//					incoming.Client.Blocking = false;
					await client.ConnectAsync ("imap.gmail.com", 993);

					log.Warn("Got connection");

					var rawStream = client.GetStream ();
					var inputStream = incoming.GetStream ();

					Stream enc = null;

					try {
						var ssl = new SslStream (rawStream);

						await ssl.AuthenticateAsClientAsync ("imap.gmail.com", new X509CertificateCollection (), System.Security.Authentication.SslProtocols.Tls12,
							false
						);

						var check = ssl.IsAuthenticated;

						enc = ssl;
					} catch (Exception e) {
					}

					var clientStream = enc;
					Task.Run (async () => {
						var buffer = new byte[1024];
						while (true) {
							try {
								if (inputStream.DataAvailable) {
									var read = await inputStream.ReadAsync (buffer, 0, 1024);
									log.Debug("IN " + Encoding.UTF8.GetString(buffer, 0, read));

									await clientStream.WriteAsync (buffer, 0, read);
									await clientStream.FlushAsync ();
								} else {
									await Task.Delay (50);	
									if (!IsConnected(client.Client) || !IsConnected(incoming.Client)) {
										throw new Exception();
									}
								}
							} catch (Exception e) {
								incoming.Close();
								client.Close();

								log.Warn("Connection closed on IN read " + e.GetType().Name + " " + e.Message);
								return;
							}
						}
					});

					Task.Run (async () => {
						var buffer = new byte[1024];
						while (true) {
							try {
								
								if (rawStream.DataAvailable) {
									var read = await clientStream.ReadAsync (buffer, 0, 1024);
									log.Debug("OUT " + Encoding.UTF8.GetString(buffer, 0, read));

									await inputStream.WriteAsync (buffer, 0, read);
									await inputStream.FlushAsync ();
								} else {
									await Task.Delay (50);	
									if (!IsConnected(client.Client) || !IsConnected(incoming.Client)) {
										throw new Exception();
									}
								}
							} catch (Exception e) {
								incoming.Close();
								client.Close();

								log.Warn("Connection closed on OUT read " + e.GetType().Name + " " + e.Message);
								return;
							}
						}
					});
				}

			}).Wait ();
		}

		public static bool IsConnected(Socket socket)
		{
			try
			{
				return !(socket.Poll(1, SelectMode.SelectRead & SelectMode.SelectWrite) && socket.Available == 0);
			}
			catch (SocketException) { return false; }
		}
	}
}

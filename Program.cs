using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Collections.Generic;
using System.Threading;

namespace testimap
{
	public static class AsyncSocket {
		public static Task<Socket> AcceptAsync(this Socket socket) {
			var tsc = new TaskCompletionSource<Socket> ();

			socket.BeginAccept (asyncResult => tsc.SetResult (socket.EndAccept (asyncResult)), null);

			return tsc.Task;
		}

		public static Task<int> ReceiveAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags, out SocketError error) {
			var tsc = new TaskCompletionSource<int> ();

			socket.BeginReceive (buffer, offset, size, flags, out error, asyncResult => {
				try {
					tsc.SetResult (socket.EndReceive (asyncResult));
				} catch (Exception e) {
					tsc.SetException (e);
				}
			}, null);

			return tsc.Task;
		}

		public static Task<int> SendAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags, out SocketError error) {
			var tsc = new TaskCompletionSource<int> ();

			socket.BeginSend (buffer, offset, size, flags, out error, asyncResult => {
				try {
					tsc.SetResult (socket.EndSend (asyncResult));
				} catch (Exception e) {
					tsc.SetException (e);
				}
			}, null);

			return tsc.Task;
		}
	}

	class Session : IDisposable
	{
		Socket socket;
		Socket destination;
		Action<Session> onDispose;

		SslStream socketSslStream;
		SslStream destinationSslStream;

		private readonly string destinationHost;
		private readonly int destinationPort;
		private readonly X509Certificate localServerCertificate;

		public Session (Socket socket, 
			Action<Session> onDispose,
			string destinationHost,
			int destinationPort,
			X509Certificate localServerCertificate
		)
		{
			this.localServerCertificate = localServerCertificate;
			this.destinationPort = destinationPort;
			this.destinationHost = destinationHost;
			this.onDispose = onDispose;
			this.socket = socket;
			this.destination = new Socket (SocketType.Stream, ProtocolType.Tcp);

			Task.Run (this.IO);
		}

		public async Task IO () {
			const int bufferSize = 4096;
			var log = NLog.LogManager.GetCurrentClassLogger ();

			var cts = new CancellationTokenSource ();

			try {
				this.socket.ReceiveBufferSize = bufferSize;
				this.socket.SendBufferSize = bufferSize;

				this.destination.ReceiveBufferSize = bufferSize;
				this.destination.SendBufferSize = bufferSize;

				var dns = await Dns.GetHostEntryAsync(this.destinationHost);
				this.destination.Connect(IPAddress.Parse(dns.AddressList[0].ToString()), this.destinationPort);

				var socketStream = new NetworkStream(this.socket, true);
				this.socketSslStream = new SslStream (socketStream);

				var destinationStream = new NetworkStream(this.destination, true);
				this.destinationSslStream = new SslStream (destinationStream);

				await this.socketSslStream.AuthenticateAsServerAsync(this.localServerCertificate);
				await this.destinationSslStream.AuthenticateAsClientAsync(this.destinationHost);

				log.Info("handshakes are ok, starting pipes");

				var reader = Task.Run (async () => {
					var buffer = new byte[bufferSize];

					int read = 0;
//					SocketError error;

					try {
						while (!cts.Token.IsCancellationRequested) {
//							read = await this.socket.ReceiveAsync (buffer, 0, bufferSize, SocketFlags.None, out error);
							read = await this.socketSslStream.ReadAsync (buffer, 0, bufferSize, cts.Token);

//							if (error != SocketError.Success) {
//								break;
//							}

							if (read > 0) {
//								await this.destination.SendAsync(buffer, 0, read, SocketFlags.None, out error);
								await this.destinationSslStream.WriteAsync(buffer, 0, read, cts.Token);
								await this.destinationSslStream.FlushAsync(cts.Token);

								log.Trace("READER: " + System.Text.Encoding.UTF8.GetString(buffer,0, read));

//								if (error != SocketError.Success) {
//									break;
//								}
							} else {
								break;
							}
						}
					} finally {
						log.Warn ("exiting reader");
					}
				});

				var writer = Task.Run (async () => {
					var buffer = new byte[bufferSize];

					int read = 0;
//					SocketError error;

					try {
						while (!cts.Token.IsCancellationRequested) {
//							read = await this.destination.ReceiveAsync (buffer, 0, bufferSize, SocketFlags.None, out error);
							read = await this.destinationSslStream.ReadAsync (buffer, 0, bufferSize, cts.Token);

//							if (error != SocketError.Success) {
//								break;
//							}

							if (read > 0) {
//								await this.socket.SendAsync(buffer, 0, read, SocketFlags.None, out error);
								await this.socketSslStream.WriteAsync(buffer, 0, read, cts.Token);
								await this.socketSslStream.FlushAsync(cts.Token);

								log.Trace("WRITER: " + System.Text.Encoding.UTF8.GetString(buffer,0, read));

//								if (error != SocketError.Success) {
//									break;
//								}
							} else {
								break;
							}
						}
					} finally {
						log.Warn ("exiting writer");
					}
				});

				await Task.WhenAny (new [] { reader, writer });
			} catch (Exception e) {
				log.Error (e.Message);
			} finally {
				log.Warn ("Closing socket");
				cts.Cancel ();

				this.Dispose ();
			}
		}

		public void Dispose ()
		{
			this.onDispose (this);

			if (this.destinationSslStream != null) {
				this.destinationSslStream.Dispose ();
			}
			if (this.socketSslStream != null) {
				this.socketSslStream.Dispose ();
			}

			this.socket.Dispose ();
			this.destination.Dispose ();
		}
	}

	class ProxyServer : IDisposable {
		public void Dispose ()
		{
			this.listener.Dispose ();
			foreach (var session in sessions) {
				session.Dispose ();
			}
		}

		private readonly int localPort;
		private readonly Socket listener;
		private readonly List<Session> sessions = new List<Session>();
		private readonly object sync = new object();
		private readonly X509Certificate localServerCertificate;
		private readonly string destinationHost;
		private readonly int destinationPort;
		const int backlogSize = 1024 * 16;

		public ProxyServer (int localPort, X509Certificate localServerCertificate, string destinationHost, int destinationPort)
		{
			this.destinationPort = destinationPort;
			this.destinationHost = destinationHost;
			this.localServerCertificate = localServerCertificate;
			this.localPort = localPort;
			listener = new Socket (SocketType.Stream, ProtocolType.Tcp);
		}

		private void AddSession (Session session) {
			lock (sync) {
				sessions.Add (session);
			}
		}

		private void RemoveSession (Session session) {
			lock (sync) {
				sessions.Remove (session);
			}
		}

		public async Task StartLisnening () {
			listener.Bind (new IPEndPoint (IPAddress.Any, this.localPort));
			listener.Listen (backlogSize);

			while (true) {
				var acceptedSocket = await listener.AcceptAsync ();
				var acceptedSession = new Session (acceptedSocket, RemoveSession, this.destinationHost, this.destinationPort, this.localServerCertificate);

				sessions.Add (acceptedSession);
			}
		}
	}

	class MainClass
	{
		public static void Main (string[] args)
		{
			string host, slport, sport, pfx;

			#if DEBUG
			NLog.LogManager.Configuration.LoggingRules.Insert(0, new NLog.Config.LoggingRule("*", NLog.LogLevel.Trace, NLog.LogManager.Configuration.FindTargetByName("cc")));
			#else
			NLog.LogManager.Configuration.LoggingRules.Insert(0, new NLog.Config.LoggingRule("*", NLog.LogLevel.Info,  NLog.LogManager.Configuration.FindTargetByName("cc")));
			#endif

			if (args.Length < 3) {
				throw new ArgumentException ("use args: proxyport host port");
			}

			slport = args [0];
			host = args [1];
			sport = args [2];

			pfx = null;
			X509Certificate serverCert = null;
			var log = NLog.LogManager.GetCurrentClassLogger ();

			if (args.Length > 3) {
				pfx = args [3];
				serverCert = new X509Certificate2 (pfx);

				log.Warn ("loaded certificate : " + serverCert.Subject);
			} else {
				log.Fatal ("can not start in insecure mode");
				return;
			}

			int localPort = int.Parse (slport);
			int port = int.Parse (sport);


			var proxy = new ProxyServer (localPort, serverCert, host, port);
			var start = proxy.StartLisnening ();

			log.Info ("listening on port 0.0.0.0:" + localPort);

			start.Wait ();
		}
	}
}

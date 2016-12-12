using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using SDDropboxShared;

namespace SDDropboxRegisterServer
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var config = ConfigurationFactory.ParseString(@"
akka {
    actor {
        provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
    }

    remote {
        helios.tcp {
            port = 8080
            hostname = localhost
        }
    }
}"); ;

            var system = ActorSystem.Create("SDDropbox", config);
            var executor = system.ActorOf<ExecutorActor>("register");

            Console.WriteLine(executor);
            Console.WriteLine("Pressione ENTER para sair...");
            Console.ReadLine();
        }

    }

    public sealed class RequestMessage
    {
        public RequestMessage(RequestMethod method, IActorRef target, IActorRef server)
        {
            this.method = method;
            this.Target = target;
            this.server = server;
        }

        public RequestMethod method { get; }
        public IActorRef Target { get; }
        public IActorRef server { get; }
    }

    public class ExecutorActor : TypedActor, IHandle<RegisterMessage>
    {
        public static List<IActorRef> _servers = new List<IActorRef>();
        private Dictionary<RequestMethod, IActorRef> _workers;

        protected override void PreStart()
        {
            base.PreStart();

            _workers = new Dictionary<RequestMethod, IActorRef>
            {
                [RequestMethod.RegisterServer] = Context.ActorOf<RegisterActor>("register_server"),
                [RequestMethod.RequestServer] = Context.ActorOf<RequestActor>("request_server")
            };
        }

        public void Handle(RegisterMessage message)
        {
            
            _workers[message.method].Tell(new RequestMessage(message.method, Sender, message.target));
            /*
            if(message.method == RequestMethod.RegisterServer){
                
            }else{
                _workers[message.method].Tell(new RequestMessage(message.method, Sender, null));
            }*/
        }
        

        // Responsable for register servers
        //=========================================================
        private class RegisterActor : TypedActor, IHandle<RequestMessage>
        {
            public void Handle(RequestMessage message)
            {
                _servers.Add(message.server);
                message.Target.Tell(new RegisterResponseMessage(message.server));
            }
        }


        // Responsable for response the requests
        //=========================================================
        private class RequestActor : TypedActor, IHandle<RequestMessage>
        {
            public void Handle(RequestMessage message)
            {   
                IActorRef server = null;
                if(_servers.Count > 0){
                    var serverId = new Random().Next(_servers.Count);
                    server = _servers[serverId];
                }
                message.Target.Tell(new RegisterResponseMessage(server));
            }
        }
       
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using SDDropboxShared;

namespace SDDropboxServer
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
            port = 0
            hostname = localhost
        }
    }
}"); ;

            var system = ActorSystem.Create("SDDropbox", config);
            var executor = system.ActorOf<OperatorActor>("executor");
            var register = system.ActorSelection("akka.tcp://SDDropbox@localhost:8080/user/register");

            RegisterResponseMessage result = null;
            Task.Run(async () => {
                result = await register.Ask<RegisterResponseMessage>(new RegisterMessage(RequestMethod.RegisterServer, executor));
            }).Wait();
            
            Console.WriteLine(result.target == null ? "Não foi possivel registrar servidor" : "Servidor registrado com sucesso!");
            
            
            Console.WriteLine(executor);
            Console.WriteLine("Pressione ENTER para sair...");
            Console.ReadLine();
        }

    }

    public sealed class OperationMessage
    {
        public OperationMessage(Operation operation, IActorRef target)
        {
            Operation = operation;
            Target = target;
        }

        public Operation Operation { get; }
        public IActorRef Target { get; }
    }

    public class OperatorActor : TypedActor, IHandle<Operation>
    {
        private Dictionary<OperationType, IActorRef> _workers;

        protected override void PreStart()
        {
            base.PreStart();

            _workers = new Dictionary<OperationType, IActorRef>
            {
                [OperationType.List] = Context.ActorOf<ListActor>("list"),
                [OperationType.Read] = Context.ActorOf<ListActor>("read"),
                [OperationType.Write] = Context.ActorOf<ListActor>("write"),
                [OperationType.Delete] = Context.ActorOf<ListActor>("delete"),
            };
        }

        public void Handle(Operation message)
        {
            var responsibleActor = _workers[message.operationType];
            var operationMessage = new OperationMessage(message, Sender);

            responsibleActor.Tell(operationMessage);
        }

        private class ListActor : TypedActor, IHandle<OperationMessage>
        {
            public void Handle(OperationMessage message)
            {
                string [] fileNames = Directory.GetFiles(Constants.FILEPATH);
                string files = String.Join("\n", fileNames);
                message.Target.Tell(files);
            }
        }

       
    }
}

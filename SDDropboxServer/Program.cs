using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

            if(!Directory.Exists(Constants.FILEPATH)){
                Directory.CreateDirectory(Constants.FILEPATH);
            }

            var system = ActorSystem.Create("SDDropbox", config);
            var executor = system.ActorOf<OperatorActor>("executor");
            var register = system.ActorSelection("akka.tcp://SDDropbox@localhost:8080/user/register");

            RegisterResponseMessage result = null;
            Task.Run(async () => {
                result = await register.Ask<RegisterResponseMessage>(new RegisterMessage(RequestMethod.RegisterServer, executor));
            }).Wait();
            
            Console.WriteLine(result.target == null ? "Não foi possivel registrar servidor" : "Servidor registrado com sucesso!");
            
            
            //List server registered
            Task.Run(async () => {
                var res = await register.Ask<List<IActorRef>>(new RegisterMessage(RequestMethod.ListServers, null));
                Console.WriteLine("Servidores registrados: {0}", res);
            }).Wait();

            

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
                [OperationType.Read] = Context.ActorOf<ReadActor>("read"),
                [OperationType.Write] = Context.ActorOf<WriteActor>("write"),
                [OperationType.Delete] = Context.ActorOf<DeleteActor>("delete"),
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

        private class WriteActor : TypedActor, IHandle<OperationMessage>
        {
            public void Handle(OperationMessage message)
            {
                var path = Constants.FILEPATH + "/" + message.Operation.filename;
                
                if(File.Exists(path)){   
                    message.Target.Tell("Arquivo ja existente!");
                }else{
                    
                    using (FileStream fs = File.Create(path))
                    {
                        //Byte[] info = new UTF8Encoding(true).GetBytes(message.Operation.content);
                        fs.Write(message.Operation.content, 0, message.Operation.content.Length);
                    }

                }

                string [] fileNames = Directory.GetFiles(Constants.FILEPATH);
                string files = String.Join("\n", fileNames);
                message.Target.Tell(files);
            }
        }


        private class ReadActor : TypedActor, IHandle<OperationMessage>
        {
            public void Handle(OperationMessage message)
            {
                var path = Constants.FILEPATH + "/" + message.Operation.filename;
                
                if(!File.Exists(path)){   
                    message.Target.Tell(new ReadResponse(false, "Arquivo não encontrado!", null, null));
                }else{
                    Console.WriteLine("Enviando: {0}", message.Operation.filename);
                    byte[] info = File.ReadAllBytes(path);
                    message.Target.Tell(new ReadResponse(true, "Arquivo sincronizado!", message.Operation.filename, info));
                }
            }
        }


        private class DeleteActor : TypedActor, IHandle<OperationMessage>
        {
            public void Handle(OperationMessage message)
            {
                if(File.Exists(Constants.FILEPATH + "/" + message.Operation.filename)){
                    File.Delete(Constants.FILEPATH + "/" + message.Operation.filename);   
                    message.Target.Tell("Arquivo removido com sucesso!");
                }else{
                    message.Target.Tell("Arquivo não existe!");
                }
            }
        }

       
    }
}

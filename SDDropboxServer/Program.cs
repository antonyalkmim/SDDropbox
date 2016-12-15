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

        public static ActorSelection register;
        public static IActorRef executor;

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

            if(args.Length == 0){
                Console.WriteLine("É necessário informar o endereço do servidor de registros");
                Console.WriteLine("--------------------------------------");
                Console.WriteLine("Finalizando SDDropbox Client!");
                Console.ReadLine();
                return;
            }
            
            if(!Directory.Exists(Constants.FILEPATH)){
                Directory.CreateDirectory(Constants.FILEPATH);
            }

            var system = ActorSystem.Create("SDDropbox", config);
            executor = system.ActorOf<OperatorActor>("executor");
            register = system.ActorSelection(String.Format("akka.tcp://SDDropbox@{0}/user/register", args[0]));

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

    public sealed class ServerOperationMessage
    {
        public ServerOperationMessage(Operation operation, IActorRef target)
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
            if(message.isServerAction){ //Client request
                responsibleActor.Tell(new ServerOperationMessage(message, Sender));
            }else{ //server request
                responsibleActor.Tell(new OperationMessage(message, Sender));
            }
            
        }

        private class ListActor : TypedActor, IHandle<OperationMessage>, IHandle<ServerOperationMessage>
        {
            public void Handle(ServerOperationMessage message)
            {
                string [] fileNames = Directory.GetFiles(Constants.FILEPATH);
                string files = String.Join("\n", fileNames);
                message.Target.Tell(files);
            }

            public void Handle(OperationMessage message)
            {
                List<IActorRef> servers = null;
                List<String> list = new List<String>();
                
                string [] fileNames = Directory.GetFiles(Constants.FILEPATH);
                list.Add(String.Join("\n", fileNames));

                Task.Run(async () => {
                    servers = await register.Ask<List<IActorRef>>(new RegisterMessage(RequestMethod.ListServers, null));
                    servers = (servers != null) ? servers : new List<IActorRef>(); 
                    Console.WriteLine("Servidores encontrados: {0}", servers.Count);

                    foreach(IActorRef serv in servers){
                        if(serv == executor) continue;
                        var res = await serv.Ask<string>(new Operation(OperationType.List, null, null, true));
                        list.Add(res);
                    }

                }).Wait();
                
                var files = String.Join("\n", list);
                message.Target.Tell(files);
            }


        }

        private class WriteActor : TypedActor, IHandle<OperationMessage>
        {
            public void Handle(OperationMessage message)
            {
                var path = Constants.FILEPATH + "/" + message.Operation.filename;
                
                List<IActorRef> servers = null;
                
                //delete local
                if(File.Exists(path)){
                    File.Delete(path);
                }
                //delete from all servers
                Task.Run(async () => {
                    servers = await register.Ask<List<IActorRef>>(new RegisterMessage(RequestMethod.ListServers, null));
                    servers = (servers != null) ? servers : new List<IActorRef>(); 
                    Console.WriteLine("Servidores encontrados: {0}", servers.Count);

                    foreach(IActorRef serv in servers){
                        if(serv == executor) continue;
                        await serv.Ask<string>(new Operation(OperationType.Delete, message.Operation.filename, null, true));
                    }
                }).Wait();
                

                using (FileStream fs = File.Create(path))
                {
                    fs.Write(message.Operation.content, 0, message.Operation.content.Length);
                }

                message.Target.Tell("Arquivo salvo com sucesso!");
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


        private class DeleteActor : TypedActor, IHandle<OperationMessage>, IHandle<ServerOperationMessage>
        {
            public void Handle(ServerOperationMessage message)
            {
                if(File.Exists(Constants.FILEPATH + "/" + message.Operation.filename)){
                    File.Delete(Constants.FILEPATH + "/" + message.Operation.filename);   
                }
                message.Target.Tell("Arquivo removido com sucesso!");
            }


            public void Handle(OperationMessage message)
            {
                List<IActorRef> servers = null;
                String path = Constants.FILEPATH + "/" + message.Operation.filename;

                //Delete local
                if(File.Exists(path)){
                    File.Delete(path);
                }else{
                    //Delete from other servers
                    Task.Run(async () => {
                        servers = await register.Ask<List<IActorRef>>(new RegisterMessage(RequestMethod.ListServers, null));
                        servers = (servers != null) ? servers : new List<IActorRef>(); 
                        Console.WriteLine("Servidores encontrados: {0}", servers.Count);

                        foreach(IActorRef serv in servers){
                            if(serv == executor) continue;
                            await serv.Ask<string>(new Operation(OperationType.Delete, message.Operation.filename, null, true));
                        }

                    }).Wait();
                }

                message.Target.Tell("Arquivo removido com sucesso!");
                
            }
        }

       
    }
    }
}

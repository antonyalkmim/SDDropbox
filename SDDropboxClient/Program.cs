using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using SDDropboxShared;

class Program
{
    static void Main(string[] args)
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

        var system = ActorSystem.Create("SDDropbox", config);
        var register = system.ActorSelection(String.Format("akka.tcp://SDDropbox@{0}/user/register", args[0]));
        
        
        while(true){
            Operation op = GetOperation();
            
            if(op == null) {
                Console.WriteLine("--------------------------------------");
                Console.WriteLine("Pressione qualquer tecla para prosseguir...");
                Console.ReadLine();
                continue;
            }else if(op.operationType == OperationType.Exit) break;

            ExecuteOperation(register, op).Wait();    
        }
        
        Console.WriteLine("Finalizando SDDropbox Client!");
        Console.ReadLine();
    }


    private static async Task ExecuteOperation(ActorSelection register, Operation operation)
    {
        //request some server
        var requestResult = await register.Ask<RegisterResponseMessage>(new RegisterMessage(RequestMethod.RequestServer, null));
        
        if(requestResult.target == null){
            Console.WriteLine("Não existe servidores disponíveis no momento!");   
        }else{
            //Console.WriteLine("Conected to: {0}", requestResult.target);

            var result = "";
            //execute operation
            switch(operation.operationType){
                case OperationType.Read:
                    var response = await requestResult.target.Ask<ReadResponse>(operation);
                    result = response.description;
                    
                    var path = Constants.FILEPATH + "/" + response.filename;
                    if(response.status){
                        if(!Directory.Exists(Constants.FILEPATH)){
                            Directory.CreateDirectory(Constants.FILEPATH);
                        }
                        if(File.Exists(path)){
                            File.Delete(path);
                        }
                        
                        using (FileStream fs = File.Create(path))
                        {
                            fs.Write(response.file, 0, response.file.Length);
                        }
                    }

                    break;
                default:
                    result = await requestResult.target.Ask<string>(operation);
                    break;
            }
            
            Console.WriteLine(result);
            //Console.WriteLine("Retorno: {0}", result);
            
        }


        Console.WriteLine("--------------------------------------");
        Console.WriteLine("Pressione qualquer tecla para prosseguir...");
        Console.ReadLine();
    }


    private static Operation GetOperation(){
        Console.Clear();
        Console.WriteLine("--------------------------------------");
        Console.WriteLine("OPÇÕES:");
        Console.WriteLine("{0} - LIST", (Int32) OperationType.List);
        Console.WriteLine("{0} - READ", (Int32) OperationType.Read);
        Console.WriteLine("{0} - WRITE", (Int32) OperationType.Write);
        Console.WriteLine("{0} - DELETE", (Int32) OperationType.Delete);
        Console.WriteLine("{0} - EXIT", (Int32) OperationType.Exit);
        Console.Write("OP: ");

        OperationType op = (OperationType) Int32.Parse(Console.ReadLine());
        
        Console.WriteLine("--------------------------------------");


        
        if(op == OperationType.Exit){ //EXIT
            return new Operation(OperationType.Exit, "", null, false);
        }

        if(op == OperationType.List){ //LIST
            return new Operation(OperationType.List, "", null, false);
        }

        Console.WriteLine("Nome do arquivo: ");
        string path = Console.ReadLine();

        if (op == OperationType.Write){ //WRITE
            if(!File.Exists(path)){
                Console.WriteLine("Arquivo não encontrado!");
                return null; 
            }else{
                var filenamePath = path.Split('/');
                var filename = filenamePath[filenamePath.Length - 1];
                
                Console.WriteLine("Enviando: {0}", filename);

                byte[] info = File.ReadAllBytes(path);
                return new Operation(op, filename, info, false);
            }
        }


        return new Operation(op, path, null, false); //READ, DELETE
    }

}

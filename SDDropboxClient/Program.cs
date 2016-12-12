using System;
using System.Collections.Generic;
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

        var system = ActorSystem.Create("SDDropbox", config);
        var register = system.ActorSelection("akka.tcp://SDDropbox@localhost:8080/user/register");
        
        while(true){
            Operation op = GetOperation();
            
            if(op == null) break;

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
            Console.WriteLine("Conected to: {0}", requestResult.target);

            //execute operation
            var result = await requestResult.target.Ask<string>(operation);
            Console.WriteLine("Retorno: {0}", result);
        }


        Console.WriteLine("--------------------------------------");
        Console.WriteLine("Pressione qualquer tecla para prosseguir...");
        Console.ReadLine();
    }


    private static Operation GetOperation(){
        Console.Clear();
        Console.WriteLine("--------------------------------------");
        Console.WriteLine("OPÇÕES:");
        Console.WriteLine("0 - LIST");
        Console.WriteLine("1 - READ");
        Console.WriteLine("2 - WRITE");
        Console.WriteLine("3 - DELETE");
        Console.WriteLine("4 - EXIT");
        Console.Write("OP: ");

        int op = Int32.Parse(Console.ReadLine());
        
        Console.WriteLine("--------------------------------------");

        //sair
        if(op == 4){
            return null;
        }
        

        return new Operation(OperationType.List, "", "");
    }

}

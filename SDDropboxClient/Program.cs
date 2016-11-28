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
        var dropbox = system.ActorSelection(
            "akka.tcp://SDDropbox@localhost:8080/user/executor");

        while(true){
            Operation op = GetOperation();
            
            if(op == null) break;
            
            ExecuteOperation(dropbox, op).Wait();    
        }
        Console.WriteLine("Finalizando SDDropbox Client!");
        Console.ReadLine();
    }

    private static async Task ExecuteOperation(ActorSelection dropbox, Operation operation)
    {
        var result = await dropbox.Ask<string>(operation);
        Console.WriteLine("Retorno:");
        Console.WriteLine(result);
    }


    private static Operation GetOperation(){
        Console.WriteLine("--------------------------------------");
        Console.WriteLine("OPÇÕES:");
        Console.WriteLine("0 - LIST");
        Console.WriteLine("1 - READ");
        Console.WriteLine("2 - WRITE");
        Console.WriteLine("3 - DELETE");
        Console.WriteLine("4 - EXIT");
        Console.Write("OP: ");

        int op = Int32.Parse(Console.ReadLine());
        
        //sair
        if(op == 4){
            return null;
        }

        return new Operation(OperationType.List, "", "");
    }

}

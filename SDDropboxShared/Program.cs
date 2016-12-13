using System;
using Akka.Actor;

namespace SDDropboxShared
{
    public static class Constants {
        public static string FILEPATH = "./files";
    }
    public sealed class Operation
    {
        
        public Operation(OperationType operationType, string filename, byte[] content)
        {
            this.operationType = operationType;
            this.filename = filename;
            this.content = content;
        }

        public string filename { get; }
        public byte[] content { get; }
        public OperationType operationType { get; }
    }


    public enum OperationType : Int32 {
        List = 0, Read = 1, Write = 2, Delete = 3, Exit = 4
    }

   
    public sealed class RegisterMessage
    {
        
        public RegisterMessage(RequestMethod method, IActorRef target)
        {
            this.method = method;
            this.target = target;
        }

        public RequestMethod method { get; }
        public IActorRef target { get; }
    }

    public sealed class RegisterResponseMessage
    {
        
        public RegisterResponseMessage(IActorRef target)
        {
            this.success = target == null;
            this.target = target;
        }

        public bool success { get; }
        public IActorRef target { get; }
    }

    public enum RequestMethod {
        RegisterServer, RequestServer, ListServers
    }

}

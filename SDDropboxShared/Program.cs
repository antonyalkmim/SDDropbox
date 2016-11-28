using System;

namespace SDDropboxShared
{
    public static class Constants {
        public static string FILEPATH = "./files";
    }
    public sealed class Operation
    {
        
        public Operation(OperationType operationType, string filename, string content)
        {
            this.operationType = operationType;
            this.filename = filename;
            this.content = content;
        }

        public string filename { get; }
        public string content { get; }
        public OperationType operationType { get; }
    }

    
    public enum OperationType {
        List, Read, Write, Delete
    }

}

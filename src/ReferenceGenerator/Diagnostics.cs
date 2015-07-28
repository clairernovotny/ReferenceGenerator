using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace ReferenceGenerator
{
    class Diagnostic
    {
        public string Type { get; private set; }
        public string Code { get; private set; }
        public string File { get; protected set; }
        public int? Line { get; protected set; }
        public int? Character { get; protected set; }
        public string Message { get; protected set; }

        public Diagnostic(string type, string code)
        {
            Type = type;
            Code = code;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(File)) {
                builder.Append(File);
                if (Line.HasValue) {
                    builder.AppendFormat("({0}", Line);
                    if (Character.HasValue)
                        builder.AppendFormat(",{0}", Character);
                    builder.Append(")");
                }
                builder.Append(": ");
            }
            builder.AppendFormat("{0} {1}", Type, Code);
            if (!string.IsNullOrWhiteSpace(Message))
                builder.AppendFormat(": {0}", Message);

            return builder.ToString();
        }
    }

    class Warning : Diagnostic
    {
        public Warning(string code) : base("warning", code) { }
    }

    class Error : Diagnostic
    {
        public Error(string code) : base("error", code) { }
    }




    class ErrorWithMessage : Error
    {
        public ErrorWithMessage(Exception ex) : base("RG001")
        {

            Message = ex.Message;
        }
    }
}

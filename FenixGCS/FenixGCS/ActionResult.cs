using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FenixGCSApi
{
    public class ActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ActionResult(bool success, string message = "")
        {
            Success = success;
            Message = message;
        }
    }
    public class ActionResult<T> : ActionResult
    {
        public T Result { get; set; }
        public ActionResult(bool success, T result, string message = "") : base(success, message)
        {
            Success = success;
            Message = message;
            Result = result;
        }
    }
}

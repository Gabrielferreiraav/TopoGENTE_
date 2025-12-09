using System;
using System.Collections.Generic;
using System.Text;

namespace TopoGente.Core.Validators
{
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
        }
    }
}
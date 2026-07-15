using System;
using System.Collections.Generic;
using System.Text;

namespace TopoGente.Core.Validators
{
    public class ValidationResult
    {
        private readonly List<string> _errors = new();
        public IReadOnlyList<string> Errors => _errors.AsReadOnly();
        public bool IsValid => _errors.Count == 0;

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error)) _errors.Add(error);
        }
    }
}
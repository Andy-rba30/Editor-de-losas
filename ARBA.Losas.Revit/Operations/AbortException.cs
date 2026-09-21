using System;

namespace ARBA.Losas.Revit.Operations
{
    /// <summary>
    /// Condicion que bloquea el comando. El mensaje dice exactamente que condicion fallo y
    /// el ElementId implicado; el comando lo muestra y termina sin tocar el modelo.
    /// </summary>
    public sealed class AbortException : Exception
    {
        public AbortException(string message) : base(message) { }
    }
}

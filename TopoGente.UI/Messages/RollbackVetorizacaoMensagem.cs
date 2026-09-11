namespace TopoGente.UI.Messages
{
    public class RollbackVetorizacaoMensagem
    {
        public int StartVertexId { get; }
        public int EndVertexId { get; }
        public string Reason { get; }

        public RollbackVetorizacaoMensagem(int startVertexId, int endVertexId, string reason)
        {
            StartVertexId = startVertexId;
            EndVertexId = endVertexId;
            Reason = reason;
        }
    }
}

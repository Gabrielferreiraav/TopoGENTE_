using System;
using TopoGente.Core.Entities;

namespace TopoGente.UI.ViewModels
{
    public class LeituraViewModel : ObservableObject
    {
        public string Id { get; }
        public TipoLeitura Tipo { get; }
        
        private string _pontoVisado;
        public string PontoVisado
        {
            get => _pontoVisado;
            set => SetProperty(ref _pontoVisado, value);
        }

        private double _anguloHorizontal;
        public double AnguloHorizontal
        {
            get => _anguloHorizontal;
            set => SetProperty(ref _anguloHorizontal, value);
        }

        private double _anguloVertical;
        public double AnguloVertical
        {
            get => _anguloVertical;
            set => SetProperty(ref _anguloVertical, value);
        }

        private double _distanciaInclinada;
        public double DistanciaInclinada
        {
            get => _distanciaInclinada;
            set => SetProperty(ref _distanciaInclinada, value);
        }

        private double _alturaPrisma;
        public double AlturaPrisma
        {
            get => _alturaPrisma;
            set => SetProperty(ref _alturaPrisma, value);
        }

        private string _observacao;
        public string Observacao
        {
            get => _observacao;
            set => SetProperty(ref _observacao, value);
        }

        public LeituraViewModel(LeituraEstacaoTotal leituraOriginal)
        {
            Id = leituraOriginal.Id;
            Tipo = leituraOriginal.Tipo;
            
            _pontoVisado = leituraOriginal.PontoVisado;
            _anguloHorizontal = leituraOriginal.AnguloHorizontal;
            _anguloVertical = leituraOriginal.AnguloVertical;
            _distanciaInclinada = leituraOriginal.DistanciaInclinada;
            _alturaPrisma = leituraOriginal.AlturaPrisma;
            _observacao = leituraOriginal.Observacao;
        }
    }
}

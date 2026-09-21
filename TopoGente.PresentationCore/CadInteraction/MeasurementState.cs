using System;
using TopoGente.UI.Spatial;

namespace TopoGente.UI.CadInteraction
{
    public class MeasurementState : CadCanvasState
    {
        private KdNode? _startNode;
        private double? _startX;
        private double? _startY;
        private double? _startZ;

        public MeasurementState(CadStateMachine stateMachine, ICadCanvasContext context) 
            : base(stateMachine, context)
        {
        }

        public override void OnMouseDown(double modelX, double modelY, KdNode? nearestNode = null)
        {
            if (!_startX.HasValue)
            {
                if (nearestNode != null)
                {
                    _startNode = nearestNode;
                    _startX = nearestNode.Value.X;
                    _startY = nearestNode.Value.Y;
                    _startZ = Context.GetElevation(nearestNode.Value.DomainId);
                }
                else
                {
                    _startX = modelX;
                    _startY = modelY;
                    _startZ = 0.0;
                }
            }
            else
            {
                // 2º clique: congela medição e volta ao repouso
                Context.ClearMeasurementBand();
                StateMachine.ChangeState(new MeasurementState(StateMachine, Context));
            }
        }

        public override void OnMouseMove(double modelX, double modelY, KdNode? nearestNode)
        {
            if (_startX.HasValue && _startY.HasValue)
            {
                double currentX = nearestNode != null ? nearestNode.Value.X : modelX;
                double currentY = nearestNode != null ? nearestNode.Value.Y : modelY;
                double currentZ = nearestNode != null ? (Context.GetElevation(nearestNode.Value.DomainId) ?? 0.0) : 0.0;
                
                Context.SetMeasurementBand(_startX, _startY, currentX, currentY);

                double dx = currentX - _startX.Value;
                double dy = currentY - _startY.Value;
                
                double dh = Math.Sqrt(dx * dx + dy * dy);
                
                double dz = currentZ - (_startZ ?? 0.0);
                
                double di = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                
                double inclinacao = dh > 0 ? (dz / dh) * 100.0 : 0.0;
                
                double azimute = (Math.Atan2(dx, dy) * 180.0 / Math.PI + 360.0) % 360.0;
                
                Context.NotificarMedicao(dh, di, dz, inclinacao, azimute);
            }
        }

        public override void OnRightClick()
        {
            Context.ClearMeasurementBand();
            Context.LimparMedicao();
            StateMachine.ChangeState(new SelectionState(StateMachine, Context));
        }

        public override void OnKeyDown(CadInteractionKey key)
        {
            if (key == CadInteractionKey.Escape)
            {
                Context.ClearMeasurementBand();
                Context.LimparMedicao();
                StateMachine.ChangeState(new SelectionState(StateMachine, Context));
            }
        }
    }
}

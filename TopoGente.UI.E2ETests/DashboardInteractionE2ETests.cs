using System;
using System.Threading;
using System.Windows;
using Xunit;
using TopoGente.UI.ViewModels;
using TopoGENTE.Infrastructure.Adapters;
using TopoGente.UI.CadInteraction;
using TopoGENTE.Domain.ValueObjects;
using TopoGente.UI.Spatial;
using TopoGENTE.Domain.Ports;
using TopoGente.Core.Entities;

namespace TopoGENTE.Test.E2E
{
    [Collection("STA UI Tests")]
    public class DashboardInteractionE2ETests
    {
        private static readonly object _appLock = new();

        [Fact]
        public void Deve_Executar_Fluxo_Completo_De_Vetorizacao_E_Undo_Sem_Excecao_Na_STA()
        {
            Exception? testEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    lock (_appLock)
                    {
                        if (Application.Current == null)
                        {
                            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        }
                    }

                    var frame = new System.Windows.Threading.DispatcherFrame();

                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                    {
                        try
                        {
                            var triangulator = new MockTriangulator();
                            var vm = new DashboardViewModel(() => triangulator, () => triangulator, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
                            
                            var cmd = new TopoGente.UI.Commands.AddBreaklineCommand(triangulator, new Breakline(1, 2));
                            vm.ExecuteCommand(cmd);

                            Assert.Single(triangulator.Breaklines);
                            
                            Assert.True(vm.UndoCommand.CanExecute(null));
                            vm.UndoCommand.Execute(null);

                            Assert.Empty(triangulator.Breaklines);
                        }
                        catch (Exception innerEx)
                        {
                            testEx = innerEx;
                        }
                        finally
                        {
                            frame.Continue = false;
                        }
                    }));

                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                }
                catch (Exception ex)
                {
                    testEx = ex;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            if (testEx != null) throw testEx;
        }

        [Fact]
        public void Undo_E_Redo_Devem_Respeitar_Invariante_De_Pilha_Vazia()
        {
            Exception? testEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    lock (_appLock)
                    {
                        if (Application.Current == null)
                        {
                            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        }
                    }

                    var frame = new System.Windows.Threading.DispatcherFrame();

                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                    {
                        try
                        {
                            var triangulator = new MockTriangulator();
                            var vm = new DashboardViewModel(() => triangulator, () => triangulator, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

                            // Invariante 1: Sem breaklines adicionadas, Undo e Redo devem ter CanExecute == false
                            Assert.False(vm.UndoCommand.CanExecute(null));
                            Assert.False(vm.RedoCommand.CanExecute(null));

                            // Adiciona uma breakline
                            var cmd = new TopoGente.UI.Commands.AddBreaklineCommand(triangulator, new Breakline(1, 2));
                            vm.ExecuteCommand(cmd);

                            // Invariante 2: Com 1 breakline, Undo = true, Redo = false
                            Assert.True(vm.UndoCommand.CanExecute(null));
                            Assert.False(vm.RedoCommand.CanExecute(null));

                            // Executa Undo
                            vm.UndoCommand.Execute(null);

                            // Invariante 3: Apos Undo, Undo = false, Redo = true
                            Assert.False(vm.UndoCommand.CanExecute(null));
                            Assert.True(vm.RedoCommand.CanExecute(null));

                            // Executa Redo
                            vm.RedoCommand.Execute(null);

                            // Invariante 4: Apos Redo, Undo = true, Redo = false
                            Assert.True(vm.UndoCommand.CanExecute(null));
                            Assert.False(vm.RedoCommand.CanExecute(null));
                        }
                        catch (Exception innerEx)
                        {
                            testEx = innerEx;
                        }
                        finally
                        {
                            frame.Continue = false;
                        }
                    }));

                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                }
                catch (Exception threadEx)
                {
                    testEx = threadEx;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            if (testEx != null) throw testEx;
        }

        private class MockTriangulator : ITerrainTriangulator, ITopographicAnalytics
        {
            public System.Collections.Generic.List<Breakline> Breaklines { get; } = new();

            public void AddConstraint(Breakline breakline) => Breaklines.Add(breakline);
            public void RemoveConstraint(Breakline breakline) => Breaklines.Remove(breakline);
            public System.Collections.Generic.IEnumerable<Breakline> GetActiveBreaklines() => Breaklines;
            public bool ExisteBreakline(Breakline breakline) => Breaklines.Contains(breakline);

            public (TerrainVertex[] Vertices, TerrainTriangle[] Triangles) GenerateBaseDelaunayMesh(ReadOnlySpan<TerrainVertex> rawPoints, ReadOnlySpan<Breakline> topographicBreaklines, double toleranceThreshold)
                => (Array.Empty<TerrainVertex>(), Array.Empty<TerrainTriangle>());
            
            public (TerrainVertex[] Vertices, TerrainTriangle[] Triangles) GetCurrentMesh()
                => (Array.Empty<TerrainVertex>(), Array.Empty<TerrainTriangle>());
            
            public void InsertVertex(TerrainVertex vertex) { }
            public void RemoveVertex(int vertexId) { }

            public System.Collections.Generic.IEnumerable<Isoline> ComputeContourMap(double stepInterval, double anchorElevation)
                => Array.Empty<Isoline>();
            
            public double InterpolateExactElevationUsingSibson(double easting, double northing) => 0;
            public double[,] RasterizarGridParalelo(double xMin, double yMin, double xMax, double yMax, int colunas, int linhas) => new double[0,0];
            public void Dispose() { }
        }
    }
}
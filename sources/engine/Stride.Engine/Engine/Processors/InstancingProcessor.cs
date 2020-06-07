using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceWire.TcpIp;
using Stride.Core.Annotations;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;
using Stride.Core.Threading;
using Stride.Engine.Rendering;
using Stride.Games;
using Stride.Rendering;

namespace Stride.Engine.Processors
{
    public class InstancingProcessor : EntityProcessor<InstancingComponent, InstancingProcessor.InstancingData>, IEntityComponentRenderProcessor
    {
        private readonly Dictionary<ModelComponent, InstancingComponent> modelInstancingMap = new Dictionary<ModelComponent, InstancingComponent>();

        public class InstancingData
        {
            public TransformComponent TransformComponent;
            public ModelComponent ModelComponent;
        }

        public InstancingProcessor() 
            : base (typeof(TransformComponent), typeof(ModelComponent)) // Requires TransformComponent and ModelComponent
        {
            // After TransformProcessor but before ModelRenderProcessor
            Order = -100;
        }

        public VisibilityGroup VisibilityGroup { get; set; }

        public override void Draw(RenderContext context)
        {
            // Process the components
            Dispatcher.ForEach(ComponentDatas, entity =>
            {
                UpdateInstancing(entity.Key, entity.Value);
            });
        }

        private void UpdateInstancing(InstancingComponent instancingComponent, InstancingData instancingData)
        {
            if (instancingComponent.Enabled && instancingComponent.Type != null)
            {
                var instancing = instancingComponent.Type;

                // Calculate inverse world and bounding box
                instancing.Update();

                if (instancingData.TransformComponent != null && instancingData.ModelComponent != null)
                {
                    // Bounding box
                    var meshCount = instancingData.ModelComponent.MeshInfos.Count;
                    for (int i = 0; i < meshCount; i++)
                    {
                        var mesh = instancingData.ModelComponent.Model.Meshes[i];
                        var meshInfo = instancingData.ModelComponent.MeshInfos[i];

                        // This must reflect the transformations in the shaders
                        var ibb = new BoundingBoxExt(instancing.BoundingBox);
                        var mbb = new BoundingBoxExt(meshInfo.BoundingBox);

                        // We need to remove the world transformation component
                        //if (mesh.Skinning != null && instancingData.ModelComponent.Skeleton != null)
                        {
                            Matrix.Invert(ref instancingData.ModelComponent.Skeleton.NodeTransformations[0].LocalMatrix, out var invWorld);
                            mbb.Transform(invWorld);
                        }

                        // ibb.Transform(instancingData.TransformComponent.WorldMatrix);
                        var center = ibb.Center;// - instancingData.TransformComponent.WorldMatrix.TranslationVector;
                        var extend = mbb.Extent + ibb.Extent;
                        meshInfo.BoundingBox = new BoundingBox(center - extend, center + extend);
                    } 
                }
            }
        }

        protected override void OnEntityComponentAdding(Entity entity, [NotNull] InstancingComponent component, [NotNull] InstancingData data)
        {
            data.TransformComponent = component.Entity.Get<TransformComponent>();
            data.ModelComponent = component.Entity.Get<ModelComponent>();

            if (data.ModelComponent != null)
                modelInstancingMap[data.ModelComponent] = component;
        }

        protected override void OnEntityComponentRemoved(Entity entity, [NotNull] InstancingComponent component, [NotNull] InstancingData data)
        {
            if (data.ModelComponent != null)
                modelInstancingMap.Remove(data.ModelComponent);
        }

        // Instancing data per InstancingComponent
        protected override InstancingData GenerateComponentData([NotNull] Entity entity, [NotNull] InstancingComponent component)
        {
            return new InstancingData();
        }

        protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] InstancingComponent component, [NotNull] InstancingData associatedData)
        {
            return true;
        }

        protected internal override void OnSystemAdd()
        {
            base.OnSystemAdd();
            VisibilityGroup.Tags.Set(InstancingRenderFeature.ModelToInstancingMap, modelInstancingMap);
        }

        protected internal override void OnSystemRemove()
        {
            VisibilityGroup.Tags.Remove(InstancingRenderFeature.ModelToInstancingMap);
            base.OnSystemRemove();
        }
    }
}

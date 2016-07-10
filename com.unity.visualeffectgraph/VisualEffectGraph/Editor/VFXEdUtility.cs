using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Experimental.VFX;
using UnityEngine;
using UnityEngine.Experimental.VFX;

namespace UnityEditor.Experimental
{
    internal class VFXEdUtility
    {
        public static void NewParticleSystem(VFXEdCanvas canvas, VFXEdDataSource dataSource, Vector2 mousePosition)
        {
            Vector2 pos = canvas.MouseToCanvas(mousePosition) - new Vector2(VFXEditorMetrics.NodeDefaultWidth / 2, 10);
            Vector2 systempos = pos + new Vector2(0, 200);

            VFXSpawnerNodeModel spawner = dataSource.CreateSpawnerNode(pos);
            VFXSpawnerBlockModel spawnerBlock = new VFXSpawnerBlockModel(VFXSpawnerBlockModel.Type.kConstantRate);
            spawnerBlock.GetInputSlot(0).Set(10.0f);

            dataSource.Create(spawnerBlock, spawner);

            VFXContextModel init = dataSource.CreateContext(VFXEditor.ContextLibrary.GetContext("Initialize"), systempos);
            VFXContextModel update = dataSource.CreateContext(VFXEditor.ContextLibrary.GetContext("Particle Update"), systempos);
            VFXContextModel output = dataSource.CreateContext(VFXEditor.ContextLibrary.GetContext("Billboard Output"), systempos);

            VFXBlockModel lifetime = new VFXBlockModel(VFXEditor.BlockLibrary.GetBlock<VFXBlockSetLifetimeRandom>());
            lifetime.GetInputSlot(0).Set(0.5f);
            lifetime.GetInputSlot(1).Set(2.5f);

            VFXBlockModel velocityConstant = new VFXBlockModel(VFXEditor.BlockLibrary.GetBlock<VFXBlockVelocityConstant>());
            velocityConstant.GetInputSlot(0).Set(new Vector3(0.0f,1.0f,0.0f));

            VFXBlockModel velocityRandom = new VFXBlockModel(VFXEditor.BlockLibrary.GetBlock<VFXBlockVelocityRandomVector>());
            velocityRandom.GetInputSlot(0).Set(new Vector3(1.0f,1.0f,1.0f));

            VFXBlockModel sizeRandom = new VFXBlockModel(VFXEditor.BlockLibrary.GetBlock<VFXBlockSizeRandomSquare>());
            sizeRandom.GetInputSlot(0).Set(0.25f);
            sizeRandom.GetInputSlot(1).Set(1.0f);

            dataSource.Create(lifetime, init);
            dataSource.Create(velocityConstant, init);
            dataSource.Create(velocityRandom, init);
            dataSource.Create(sizeRandom, init);

            dataSource.Create(new VFXBlockModel(VFXEditor.BlockLibrary.GetBlock<VFXBlockSetColorGradientOverLifetime>()), update);

            dataSource.ConnectContext(init, update);
            dataSource.ConnectContext(update, output);
            dataSource.ConnectSpawner(spawner, init);

            init.GetOwner().MaxNb = 100;
            init.GetOwner().SpawnRate = 10;
            init.GetOwner().BlendingMode = BlendMode.kAlpha;

            canvas.Layout();
            VFXEdLayoutUtility.LayoutSystem(init.GetOwner(), dataSource);
            canvas.ReloadData();
            canvas.Repaint();
        }

        public static void NewComment(VFXEdCanvas canvas, VFXEdDataSource dataSource, Vector2 mousePosition)
        {
            dataSource.CreateComment(canvas.MouseToCanvas(mousePosition), new Vector2(400,300),"New Comment", "", UnityEngine.Random.ColorHSV(0.0f,1.0f,0.0f,0.5f,0.2f,0.4f));
        }
    }
}

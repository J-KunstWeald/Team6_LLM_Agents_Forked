﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using com.csutil.model.ecs;
using Newtonsoft.Json;
using Xunit;

namespace com.csutil.tests.model.esc {

    public class EntityComponentSystemTests {

        public EntityComponentSystemTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        [Fact]
        public async Task ExampleUsageOfTemplatesIO() {

            var rootDir = EnvironmentV2.instance.GetOrAddTempFolder("EntityComponentSystemTests_ExampleUsage1");
            var templatesDir = rootDir.GetChildDir("Templates");
            templatesDir.DeleteV2();
            templatesDir.CreateV2();

            var templates = new TemplatesIO<Entity>(templatesDir);

            var enemyTemplate = new Entity() {
                LocalPose = Matrix4x4.CreateTranslation(1, 2, 3),
                Components = new List<IComponentData>() {
                    new EnemyComponent() { Id = "c1", Health = 100, Mana = 10 }
                }
            };
            templates.SaveAsTemplate(enemyTemplate);

            // An instance that has a different health value than the template:
            Entity variant1 = templates.CreateVariantInstanceOf(enemyTemplate);
            (variant1.Components.Single() as EnemyComponent).Health = 200;
            templates.SaveAsTemplate(variant1); // Save it as a variant of the enemyTemplate

            // Create a variant2 of the variant1
            Entity variant2 = templates.CreateVariantInstanceOf(variant1);
            (variant2.Components.Single() as EnemyComponent).Mana = 20;
            templates.SaveAsTemplate(variant2);

            // Updating variant 1 should also update variant2:
            (variant1.Components.Single() as EnemyComponent).Health = 300;
            templates.SaveAsTemplate(variant1);
            variant2 = templates.LoadTemplateInstance(variant2.Id);
            Assert.Equal(300, (variant2.Components.Single() as EnemyComponent).Health);

            {
                // Another instance that is identical to the template:
                Entity instance3 = templates.CreateVariantInstanceOf(enemyTemplate);
                // instance3 is not saved as a variant 
                // Creating an instance of an instance is not allowed:
                Assert.Throws<InvalidOperationException>(() => templates.CreateVariantInstanceOf(instance3));
                // Instead the parent template should be used to create another instance:
                Entity instance4 = templates.CreateVariantInstanceOf(templates.LoadTemplateInstance(instance3.TemplateId));
                Assert.Equal(instance3.TemplateId, instance4.TemplateId);
                Assert.NotEqual(instance3.Id, instance4.Id);
            }

            var ecs2 = new TemplatesIO<Entity>(templatesDir);

            var ids = ecs2.GetAllEntityIds().ToList();
            Assert.Equal(3, ids.Count());

            Entity v1 = ecs2.LoadTemplateInstance(variant1.Id);
            var enemyComp1 = v1.Components.Single() as EnemyComponent;
            Assert.Equal(300, enemyComp1.Health);
            Assert.Equal(10, enemyComp1.Mana);

            // Alternatively to automatically lazy loading the templates can be loaded into memory all at once: 
            await ecs2.LoadAllTemplateFilesIntoMemory();

            Entity v2 = ecs2.LoadTemplateInstance(variant2.Id);
            var enemyComp2 = v2.Components.Single() as EnemyComponent;
            Assert.Equal(300, enemyComp2.Health);
            Assert.Equal(20, enemyComp2.Mana);

        }

        [Fact]
        public async Task ExampleUsageOfEcs() {

            var ecs = new EntityComponentSystem<Entity>(null);

            var entityGroup = ecs.Add(new Entity() {
                LocalPose = Matrix4x4.CreateRotationY(MathF.PI / 2) // 90 degree rotation around y axis
            });

            var e1 = entityGroup.AddChild(new Entity() {
                LocalPose = Matrix4x4.CreateRotationY(-MathF.PI / 2), // -90 degree rotation around y axis
            }, AddToChildrenListOfParent);

            var e2 = entityGroup.AddChild(new Entity() {
                LocalPose = Matrix4x4.CreateTranslation(1, 0, 0),
            }, AddToChildrenListOfParent);

            var children = entityGroup.GetChildren();
            Assert.Equal(2, children.Count());
            Assert.Same(e1, children.First());
            Assert.Same(e2, children.Last());
            Assert.Same(e1.GetParent(), entityGroup);
            Assert.Same(e2.GetParent(), entityGroup);

            { // Local and global poses can be accessed like this:
                var rot90Degree = Quaternion.CreateFromYawPitchRoll(MathF.PI / 2, 0, 0);
                Assert.Equal(rot90Degree, entityGroup.GlobalPose().rotation);
                Assert.Equal(rot90Degree, entityGroup.LocalPose().rotation);

                // e2 does not have a local rot so the global rot is the same as of the parent:
                Assert.Equal(rot90Degree, e2.GlobalPose().rotation);
                Assert.Equal(Quaternion.Identity, e2.LocalPose().rotation);

                // e1 has a local rotation that is opposite of the parent 90 degree, the 2 cancel each other out:
                Assert.Equal(Quaternion.Identity, e1.GlobalPose().rotation);
                var rotMinus90Degree = Quaternion.CreateFromYawPitchRoll(-MathF.PI / 2, 0, 0);
                Assert.Equal(rotMinus90Degree, e1.LocalPose().rotation);

                // e1 is in the center of the parent, its global pos isnt affected by the rotation of the parent:
                Assert.Equal(Vector3.Zero, e1.GlobalPose().position);
                Assert.Equal(Vector3.Zero, e1.LocalPose().position);

                // Due to the rotation of the parent the global position of e2 is now (0,0,1):
                Assert.Equal(new Vector3(1, 0, 0), e2.LocalPose().position);
                Assert_AlmostEqual(new Vector3(0, 0, -1), e2.GlobalPose().position);

                // The scales are all 1:
                Assert.Equal(Vector3.One, e1.GlobalPose().scale);
                Assert.Equal(Vector3.One, e1.LocalPose().scale);
            }

            Assert.Equal(3, ecs.AllEntities.Count);
            e1.RemoveFromParent(RemoveChildIdFromParent);
            // e1 is removed from its parent but still in the scene graph:
            Assert.Equal(3, ecs.AllEntities.Count);
            Assert.Same(e2, entityGroup.GetChildren().Single());
            Assert.Null(e1.GetParent());
            Assert.True(e1.Destroy(RemoveChildIdFromParent));
            Assert.False(e1.Destroy(RemoveChildIdFromParent));
            // e1 is now fully removed from the scene graph and destroyed:
            Assert.Equal(2, ecs.AllEntities.Count);

            Assert.False(e2.IsDestroyed());

            var e3 = e2.AddChild(new Entity(), AddToChildrenListOfParent);
            var e4 = e3.AddChild(new Entity(), AddToChildrenListOfParent);

            Assert.True(e2.Destroy(RemoveChildIdFromParent));
            Assert.Empty(entityGroup.GetChildren());

            Assert.True(e2.IsDestroyed());
            Assert.Equal(1, ecs.AllEntities.Count);

            // Since e3 and e4 are in the subtree of e2 they are also destroyed:
            Assert.True(e3.IsDestroyed());
            Assert.True(e4.IsDestroyed());

        }

        [Fact]
        public async Task TestEcsPoseMath() {

            /* A test that composes a complex nested scene graph and checks if the
             * global pose of the most inner entity is back at the origin (validated that
             * same result is achieved with Unity) */

            var ecs = new EntityComponentSystem<Entity>(null);

            var e1 = ecs.Add(new Entity() {
                LocalPose = NewPose(new Vector3(0, 1, 0))
            });

            var e2 = e1.AddChild(new Entity() {
                LocalPose = NewPose(new Vector3(0, 1, 0), 90)
            }, AddToChildrenListOfParent);

            var e3 = e2.AddChild(new Entity() {
                LocalPose = NewPose(new Vector3(0, 0, 2), 0, 2)
            }, AddToChildrenListOfParent);

            var e4 = e3.AddChild(new Entity() {
                LocalPose = NewPose(new Vector3(0, 0, -1), -90)
            }, AddToChildrenListOfParent);

            var e5 = e4.AddChild(new Entity() {
                LocalPose = NewPose(new Vector3(0, -1, 0), 0, 0.5f)
            }, AddToChildrenListOfParent);

            var pose = e5.GlobalPose();
            Assert.Equal(Quaternion.Identity, pose.rotation);
            Assert_AlmostEqual(Vector3.One, pose.scale);
            Assert.Equal(Vector3.Zero, pose.position);

        }

        /// <summary> Shows how to create a scene at runtime, persist it to disk and reload it </summary>
        [Fact]
        public async Task ExampleRuntimeSceneCreationPersistenceAndReloading() {

            // First the user creates a scene at runtime:
            var dir = EnvironmentV2.instance.GetNewInMemorySystem();
            var templatesIo = new TemplatesIO<Entity>(dir);
            var ecs = new EntityComponentSystem<Entity>(templatesIo);

            // He defines a few of the entities as templates and other as variants

            // Define a base enemy template with a sword:
            var baseEnemy = ecs.Add(new Entity() {
                Name = "EnemyTemplate",
                Components = new[] { new EnemyComponent() { Health = 100, Mana = 0 } }
            });
            baseEnemy.AddChild(new Entity() {
                Name = "Sword",
                Components = new[] { new SwordComponent() { Damage = 10 } }
            }, AddToChildrenListOfParent);
            baseEnemy.SaveChanges();

            // Define a variant of the base enemy which is stronger and has a shield:
            var bossEnemy = baseEnemy.CreateVariant();
            bossEnemy.Data.Name = "BossEnemy";
            bossEnemy.GetComponent<EnemyComponent>().Health = 200;
            bossEnemy.AddChild(new Entity() {
                Name = "Shield",
                Components = new[] { new ShieldComponent() { Defense = 10 } }
            }, AddToChildrenListOfParent);
            bossEnemy.SaveChanges();

            // Define a mage variant that has mana but no sword
            var mageEnemy = baseEnemy.CreateVariant();
            mageEnemy.Data.Name = "MageEnemy";
            mageEnemy.GetComponent<EnemyComponent>().Mana = 100;
            mageEnemy.GetChild("Sword").RemoveFromParent(RemoveChildIdFromParent);
            mageEnemy.SaveChanges();



        }


        private Matrix4x4 NewPose(Vector3 pos, float rot = 0, float scale = 1f) {
            return Matrix4x4Extensions.Compose(pos, Quaternion.CreateFromYawPitchRoll(rot, 0, 0), new Vector3(scale, scale, scale));
        }

        private void Assert_AlmostEqual(Vector3 a, Vector3 b, float allowedDelta = 0.000001f) {
            var length = (a - b).Length();
            Assert.True(length < allowedDelta, $"Expected {a} to be almost equal to {b} but the length of the difference is {length}");
        }

        private static Entity AddToChildrenListOfParent(Entity parent, string addedChildId) {
            parent.MutablehildrenIds.Add(addedChildId);
            return parent;
        }

        private Entity RemoveChildIdFromParent(Entity parent, string childIdToRemove) {
            parent.MutablehildrenIds.Remove(childIdToRemove);
            return parent;
        }

        private class Entity : IEntityData {
            public string Id { get; set; } = "" + GuidV2.NewGuid();
            public string Name { get; set; }
            public string TemplateId { get; set; }
            public Matrix4x4? LocalPose { get; set; }
            public IReadOnlyList<IComponentData> Components { get; set; }

            [JsonIgnore]
            public List<string> MutablehildrenIds { get; } = new List<string>();
            public IReadOnlyList<string> ChildrenIds => MutablehildrenIds;

            public string GetId() { return Id; }

        }

        private class EnemyComponent : IComponentData {
            public string Id { get; set; }
            public int Mana { get; set; }
            public int Health;
            public string GetId() { return Id; }
        }

        private class SwordComponent : IComponentData {
            public string Id { get; set; }
            public int Damage { get; set; }
            public string GetId() { return Id; }
        }

        private class ShieldComponent : IComponentData {
            public string Id { get; set; }
            public int Defense { get; set; }
            public string GetId() { return Id; }
        }

    }

}
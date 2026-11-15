using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoopSort0.Core;

namespace LoopSort0.Tests
{
    public sealed class GameplayFlowTests
    {
        GameController game;GameObject owner;
        [UnitySetUp] public IEnumerator Setup()
        {
            if(GameController.Instance!=null)Object.Destroy(GameController.Instance.gameObject);
            yield return null;
            owner=new GameObject("Isolated gameplay validation");game=owner.AddComponent<GameController>();game.IsTestRun=true;
            yield return null;yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;
            if(owner!=null)Object.Destroy(owner);
            yield return null;yield return null;
        }
        [UnityTest] public IEnumerator FirstSourceLevelCompletesThroughPhysicsAndIntake()
        {
            for(int repeat=0;repeat<3;repeat++)
            {
                game.LoadLevel(1);yield return null;
                Vector3 original=game.TruckViews[1].position;
                Assert.AreEqual(4*game.State.GroupSize,game.State.TotalPieces);Assert.IsTrue(game.TryRelease(0));
                float deadline=Time.realtimeSinceStartup+25;
                while(game.State.Status==RunStatus.Playing&&Time.realtimeSinceStartup<deadline)yield return null;
                game.State.AssertConservation();Assert.AreEqual(RunStatus.Won,game.State.Status,
                    "Run "+repeat+", cargo: "+string.Join(",",game.State.Carriers.Select(c=>c.cargo.Count))+", belt: "+game.State.BeltCount);
                yield return new WaitForSeconds(1.2f);
                Assert.IsTrue(game.TruckViews[1].gameObject.activeSelf);Assert.AreEqual(original,game.TruckViews[1].position);
                Assert.IsTrue(game.TruckViews[1].Find("Completed check").gameObject.activeSelf);
                foreach(var view in game.PieceViews.Values)
                    Assert.AreEqual(game.State.Fragments.CellSize,view.transform.localScale.x*.51f,.001f,"Received cargo must fit its packed cell");
            }
        }
        [UnityTest] public IEnumerator PauseFreezesMovingCubesAndResumeRestoresFlow()
        {
            game.LoadLevel(3);Assert.IsTrue(game.TryRelease(0));float deadline=Time.realtimeSinceStartup+8;
            while(!game.State.Pieces.Any(p=>p.location==PieceLocation.Belt)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(game.State.Pieces.Any(p=>p.location==PieceLocation.Belt));
            var moving=game.State.Pieces.First(p=>p.location==PieceLocation.Belt);var view=game.PieceViews[moving.id];
            game.SetPaused(true);Vector3 position=view.transform.position;
            yield return new WaitForSeconds(.3f);Assert.Less(Vector3.Distance(position,view.transform.position),.001f);
            float travelled=view.Travelled;game.SetPaused(false);yield return new WaitForSeconds(.3f);
            Assert.Greater(view.Travelled,travelled);Assert.IsFalse(game.Paused);game.State.AssertConservation();
        }
        [UnityTest] public IEnumerator RestartWhileUnloadingCannotCreateLateCubes()
        {
            game.LoadLevel(2);game.TryRelease(1);yield return new WaitForSeconds(.1f);game.LoadLevel(1);
            yield return new WaitForSeconds(1);
            Assert.AreEqual(4*game.State.GroupSize,game.State.TotalPieces);Assert.AreEqual(0,game.State.BeltCount);Assert.AreEqual(0,game.State.PendingTransfers);game.State.AssertConservation();
        }
        [UnityTest] public IEnumerator AddTruckUsesOnlyDeclaredSpareLocations()
        {
            game.LoadLevel(1);int original=game.State.Carriers.Count;game.AddTruck();game.AddTruck();game.AddTruck();yield return null;
            Assert.AreEqual(original+game.Level.spares.Length,game.State.Carriers.Count);game.State.AssertConservation();
        }
        [UnityTest] public IEnumerator UndoRestoresCargoAfterACompletedRelease()
        {
            game.LoadLevel(3);int initial=game.State.Carriers[0].cargo.Count;
            Assert.IsTrue(game.TryRelease(0));float deadline=Time.realtimeSinceStartup+8;
            while(game.State.PendingTransfers>0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.Zero(game.State.PendingTransfers);Assert.IsTrue(game.Undo());yield return null;
            Assert.Zero(game.State.BeltCount);Assert.AreEqual(initial,game.State.Carriers[0].cargo.Count);Assert.AreEqual(1,game.Progress.undo);game.State.AssertConservation();
        }
        [Test] public void NativeLaneConstraintKeepsForwardPositionAndClampsOnlyTheSide()
        {
            var sample=new BeltSteering.RouteSample {position=new Vector3(0,1.1f,0),forward=Vector3.forward,
                up=Vector3.up,side=Vector3.left};
            var result=BeltSteering.Constrain(new Vector3(.6f,1.35f,2),sample);
            Assert.AreEqual(.4f,result.position.x,.0001f);Assert.AreEqual(1.35f,result.position.y,.0001f);
            Assert.AreEqual(2,result.position.z,.0001f);Assert.IsTrue(result.lateralClamped);Assert.IsFalse(result.reset);
        }
        [UnityTest] public IEnumerator DenseLevelNineteenCargoKeepsMovingWithPhysicalCollisions()
        {
            Time.timeScale=4;
            var source=game.Catalog.levels.First(l=>l.id==19);
            for(int i=0;i<source.carriers.Length;i++)source.carriers[i].groups=(i<9?new[]{1,1,1,2}:new[]{1,1,1,1}).Select(c=>new CargoGroup{color=c}).ToArray();
            game.LoadLevel(19);
            for(int i=0;i<9;i++)
            {
                Assert.IsTrue(game.TryRelease(i));float deadline=Time.realtimeSinceStartup+8;
                while(game.State.PendingTransfers>0&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.Zero(game.State.PendingTransfers);
            }
            Assert.AreEqual(9*game.State.GroupSize,game.State.BeltCount);
            var ids=game.State.Pieces.Where(p=>p.location==PieceLocation.Belt).Select(p=>p.id).ToArray();
            var before=ids.Select(id=>game.PieceViews[id].Travelled).ToArray();
            yield return new WaitForSeconds(12);
            int moving=ids.Where((id,i)=>game.PieceViews[id].Travelled-before[i]>10).Count();
            float average=ids.Average(id=>game.PieceViews[id].Body.velocity.magnitude);
            Debug.Log("Physical congestion check: "+moving+"/"+ids.Length+" advanced over 10 units; mean speed "+average);
            Assert.AreEqual(ids.Length,moving,"Cubes stalled under dense transport");
            Assert.Greater(average,1f);Assert.AreEqual(9*game.State.GroupSize,game.State.BeltCount);game.State.AssertConservation();
            foreach(int id in ids){Assert.IsFalse(game.PieceViews[id].Body.isKinematic);Assert.IsTrue(game.PieceViews[id].Body.detectCollisions);}
        }
        [TestCase(1)][TestCase(3)][TestCase(7)][TestCase(10)][TestCase(19)][TestCase(20)][TestCase(33)]
        public void CargoBodiesDoNotCoverTheConveyor(int level)
        {
            game.LoadLevel(level);var geometry=new TrackGeometry(game.Level);
            foreach(var truck in game.TruckViews)
            {
                var bounds=truck.Find("Pivot/Model Root/Model").GetComponent<Renderer>().bounds;
                foreach(var point in geometry.Points)
                {
                    Vector3 onPlane=new Vector3(point.x,bounds.center.y,point.z);
                    Assert.Greater(Mathf.Sqrt(bounds.SqrDistance(onPlane)),.96f,"Truck overlaps belt in level "+level);
                }
            }
        }
        [Test] public void BurstLandingUsesEachTruckOutletAndAlternatesAcrossTheLane()
        {
            game.LoadLevel(3);var geometry=new TrackGeometry(game.Level);
            Vector3 first=CargoEmission.OutletPosition(game.TruckViews[0]),second=CargoEmission.OutletPosition(game.TruckViews[1]);
            var a=Enumerable.Range(0,game.State.GroupSize).Select(i=>CargoEmission.LandingPoint(geometry,first,i)).ToArray();
            var b=Enumerable.Range(0,game.State.GroupSize).Select(i=>CargoEmission.LandingPoint(geometry,second,i)).ToArray();
            Assert.Greater(Vector3.Distance(a[0],b[0]),1f,"Each truck must use its own outlet");
            var outlets=new[]{first,second};var landings=new[]{a,b};
            for(int truck=0;truck<outlets.Length;truck++)
            {
                Vector3 origin=geometry.Nearest(outlets[truck],out Vector3 forward,out _);
                Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
                Assert.AreEqual(2,landings[truck].Distinct().Count(),"Native slots use two alternating lateral offsets");
                for(int i=0;i<landings[truck].Length;i++)
                {
                    Vector3 offset=landings[truck][i]-origin;
                    Assert.AreEqual(.25f,offset.y,.001f,"Native landing height is 0.25 above the spline");
                    Assert.AreEqual((i&1)==0?-.25f:.25f,Vector3.Dot(offset,right),.001f);
                    Assert.Less((offset-Vector3.up*.25f-right*Vector3.Dot(offset,right)).magnitude,.001f,
                        "The landing must remain beside its own outlet projection");
                }
            }
        }
        [TestCase(1)][TestCase(3)][TestCase(10)][TestCase(19)]
        public void FramingUsesThePlayAreaWithoutClipping(int level)
        {
            game.LoadLevel(level);float oldAspect=game.Camera.aspect;game.Camera.aspect=540f/960f;game.Reframe();
            foreach(var truck in game.TruckViews)
                foreach(var renderer in truck.GetComponentsInChildren<Renderer>())
                {
                    var b=renderer.bounds;
                    for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
                    {
                        Vector3 p=game.Camera.WorldToViewportPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z));
                        Assert.That(p.x,Is.InRange(.024f,.976f));Assert.That(p.y,Is.InRange(.189f,.866f));
                    }
                }
            game.Camera.aspect=oldAspect;game.Reframe();
        }
        [TestCase(1,0,3)][TestCase(1,1,1)][TestCase(3,0,2)]
        public void FrontOutlineCoversTheAvailableMatchingBlocks(int level,int carrier,int groups)
        {
            game.LoadLevel(level);
            var highlight=game.TruckViews[carrier].GetComponentsInChildren<CargoHighlight>();
            Assert.AreEqual(1,highlight.Length);Assert.AreEqual(groups*game.State.GroupSize,highlight[0].CubeCount);
            Assert.AreEqual("LoopSort0/CargoOutline",highlight[0].GetComponent<Renderer>().sharedMaterial.shader.name);
        }
        [UnityTest] public IEnumerator HiddenCoverUsesAnimatedPlanarMaterial()
        {
            game.LoadLevel(7);
            var material=game.TruckViews.SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>()).Select(r=>r.sharedMaterial)
                .First(m=>m.shader.name=="LoopSort0/HiddenCargo");
            Assert.IsTrue(material.shader.isSupported);Assert.IsNotNull(material.GetTexture("_MainTex"));
            float before=material.GetFloat("_FlowTime");yield return new WaitForSeconds(.25f);
            Assert.Greater(material.GetFloat("_FlowTime"),before);
        }
        [UnityTest] public IEnumerator BurstRigidbodiesStartAtTheirVisibleCargoPositions()
        {
            game.LoadLevel(3);Assert.IsTrue(game.TryRelease(0));
            foreach(var piece in game.State.Pieces.Where(p=>p.location==PieceLocation.Releasing))
            {
                var view=game.PieceViews[piece.id];
                Assert.Less(Vector3.Distance(view.Body.position,view.transform.position),.001f,"Stale physics origin: "+piece.id);
                Assert.Greater(view.Body.position.y,1f);Assert.Greater(view.Body.position.z,3f);
                Assert.AreEqual(RigidbodyInterpolation.None,view.Body.interpolation);
            }
            yield return new WaitForSeconds(.15f);
            foreach(var piece in game.State.Pieces.Where(p=>p.location==PieceLocation.Releasing))
            {
                var view=game.PieceViews[piece.id];
                Assert.Less(Vector3.Distance(view.Body.position,view.transform.position),.001f);
                Assert.Greater(view.Body.position.y,1f);
            }
        }
        [Test] public void EveryImportedLevelCanCreateAnExplicitBasicRulesState()
        {
            foreach(var level in game.Catalog.levels)
            {
                var state=new GameState(level,allowBasicRulesFallback:true);
                Assert.AreEqual(level.PieceCount,state.TotalPieces,"Level "+level.id);state.AssertConservation();
            }
        }
        [UnityTest] public IEnumerator EveryLevelButtonIsSelectableAndSpecialLevelsActuallyLoad()
        {
            var seen=new System.Collections.Generic.HashSet<int>();
            for(int page=0;page<(game.Catalog.levels.Length+29)/30;page++)
            {
                game.Hud.Levels(game.Catalog,game.Level.id,page);yield return null;
                var buttons=game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>()
                    .Where(b=>b.name.StartsWith("Level ")&&int.TryParse(b.name.Substring(6),out _)).ToArray();
                Assert.AreEqual(System.Math.Min(30,game.Catalog.levels.Length-page*30),buttons.Length);
                foreach(var button in buttons){Assert.IsTrue(button.interactable,button.name);seen.Add(int.Parse(button.name.Substring(6)));}
            }
            Assert.AreEqual(game.Catalog.levels.Length,seen.Count);
            foreach(int id in new[]{20,100,1299})
            {
                game.Hud.Levels(game.Catalog,game.Level.id,(id-1)/30);yield return null;
                var button=game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="Level "+id);
                button.onClick.Invoke();Assert.AreEqual(id,game.Level.id);Assert.IsFalse(game.Paused);game.State.AssertConservation();
            }
        }
        [UnityTest] public IEnumerator DifferentTrucksStartUnloadingWithoutWaitingForOtherCargo()
        {
            game.LoadLevel(3);Assert.IsTrue(game.TryRelease(0));Assert.Greater(game.State.PendingTransfers,0);
            Assert.IsTrue(game.TryRelease(1));Assert.AreEqual(4*game.State.GroupSize,game.State.BeltCount);Assert.AreEqual(1,game.UndoDepth);
            Assert.Greater(game.State.Carriers[0].releasing,0);Assert.Greater(game.State.Carriers[1].releasing,0);
            float deadline=Time.realtimeSinceStartup+8;
            while(game.State.PendingTransfers>0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.Zero(game.State.PendingTransfers);game.State.AssertConservation();
        }
        [UnityTest] public IEnumerator RapidClicksOnOneTruckAreIgnoredAndUndoCancelsTheMatchingBatch()
        {
            game.LoadLevel(3);var initial=game.State.Carriers.Select(c=>c.cargo.ToArray()).ToArray();
            var expected=initial[0].Skip(initial[0].Length-2*game.State.GroupSize).ToArray();
            Assert.IsTrue(game.TryRelease(0));
            for(int click=0;click<10;click++)Assert.IsFalse(game.TryRelease(0));
            CollectionAssert.AreEquivalent(expected,game.State.Pieces.Where(p=>p.location==PieceLocation.Releasing).Select(p=>p.id));
            Assert.AreEqual(expected.Length,game.State.BeltCount);Assert.AreEqual(1,game.UndoDepth);
            yield return new WaitForSeconds(.12f);Assert.IsTrue(game.Undo());yield return new WaitForSeconds(1);
            Assert.Zero(game.State.PendingTransfers);Assert.Zero(game.State.BeltCount);
            for(int i=0;i<initial.Length;i++)CollectionAssert.AreEqual(initial[i],game.State.Carriers[i].cargo);
            game.State.AssertConservation();
        }
        [UnityTest] public IEnumerator ClickingDuringIntakeReversesIncomingCargoWithoutTeleporting()
        {
            game.LoadLevel(2);Assert.IsTrue(game.TryRelease(0));
            float deadline=Time.realtimeSinceStartup+12;
            while(!game.State.Carriers[1].cargo.Any(i=>game.State.Pieces[i].location==PieceLocation.Receiving)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(game.State.Carriers[1].cargo.Any(i=>game.State.Pieces[i].location==PieceLocation.Receiving));
            var incoming=game.State.Carriers[1].cargo.Where(id=>game.State.Pieces[id].location==PieceLocation.Receiving).ToArray();
            var positions=incoming.Select(id=>game.PieceViews[id].transform.position).ToArray();
            Assert.IsTrue(game.TryRelease(1));Assert.Zero(game.State.Carriers[1].cargo.Count);
            for(int i=0;i<incoming.Length;i++)
            {
                Assert.AreEqual(PieceLocation.Releasing,game.State.Pieces[incoming[i]].location);
                Assert.Less(Vector3.Distance(positions[i],game.PieceViews[incoming[i]].transform.position),.001f);
            }
            deadline=Time.realtimeSinceStartup+12;
            while(game.State.PendingTransfers>0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.Zero(game.State.PendingTransfers);game.State.AssertConservation();
        }
        [UnityTest] public IEnumerator TruckUnlocksOnlyAfterEveryFragmentHasReachedTheBelt()
        {
            game.LoadLevel(3);int initial=game.State.Carriers[0].cargo.Count;
            Assert.IsTrue(game.TryRelease(0));int batch=game.State.Carriers[0].releasing;bool sawPartialLanding=false;
            float deadline=Time.realtimeSinceStartup+8;
            while(game.State.Carriers[0].releasing>0&&Time.realtimeSinceStartup<deadline)
            {
                int remaining=game.State.Carriers[0].releasing;
                if(remaining<batch)sawPartialLanding=true;
                Assert.IsFalse(game.TryRelease(0));
                Assert.AreEqual(initial-batch,game.State.Carriers[0].cargo.Count);
                Assert.Zero(game.TruckViews[0].GetComponentsInChildren<CargoHighlight>().Length);
                yield return null;
            }
            Assert.IsTrue(sawPartialLanding);Assert.Zero(game.State.Carriers[0].releasing);
            Assert.AreEqual(initial-batch,game.TruckViews[0].GetComponentInChildren<CargoHighlight>().CubeCount);
            Assert.IsTrue(game.TryRelease(0));Assert.AreEqual(initial-batch,game.State.Carriers[0].releasing);
            Assert.Zero(game.State.Carriers[0].cargo.Count);game.State.AssertConservation();
        }
        [UnityTest] public IEnumerator RestartCancelsEveryConcurrentRelease()
        {
            game.LoadLevel(3);Assert.IsTrue(game.TryRelease(0));Assert.IsTrue(game.TryRelease(1));Assert.IsTrue(game.TryRelease(2));
            yield return new WaitForSeconds(.1f);game.LoadLevel(1);yield return new WaitForSeconds(1);
            Assert.AreEqual(4*game.State.GroupSize,game.State.TotalPieces);Assert.Zero(game.State.BeltCount);Assert.Zero(game.State.PendingTransfers);game.State.AssertConservation();
        }
        [Test] public void RoundedTrackHasNoSharpCornerTangentChanges()
        {
            game.LoadLevel(1);var track=new TrackGeometry(game.Level);var points=track.Points;
            for(int i=0;i<points.Count;i++)
            {
                var a=(points[i]-points[(i+points.Count-1)%points.Count]).normalized;
                var b=(points[(i+1)%points.Count]-points[i]).normalized;
                Assert.Greater(Vector3.Dot(a,b),.994f,"Sharp tangent at "+i);
            }
        }
        [Test] public void SourceLevelTwentyHasColoredCurtainsAndHiddenContents()
        {
            game.LoadLevel(20);
            foreach(var carrier in game.State.Carriers.Where(c=>c.curtainColor>0))
            {
                var truck=game.TruckViews[carrier.index];Assert.IsFalse(carrier.curtainOpen);
                Assert.IsTrue(truck.GetComponent<CurtainView>().IsCovering);Assert.IsFalse(truck.Find("Cargo groups").gameObject.activeSelf);
                Assert.IsFalse(game.TryRelease(carrier.index));
            }
            Assert.AreEqual(3,game.State.Carriers.Count(c=>c.curtainColor>0));
        }
        [UnityTest] public IEnumerator SourceLevelTwentyMatchingCompletionRevealsCurtainAndUndoClosesIt()
        {
            game.LoadLevel(20);game.AddTruck();int count=game.State.Carriers.Count;
            Assert.IsTrue(game.TryRelease(0));Assert.IsTrue(game.TryRelease(1));
            float deadline=Time.realtimeSinceStartup+25;
            while(!game.State.Carriers[2].curtainOpen&&Time.realtimeSinceStartup<deadline)
            {
                foreach(int index in new[]{0,1})
                {
                    var truck=game.State.Carriers[index];
                    if(truck.releasing==0&&truck.cargo.Count>0&&truck.cargo.Count%game.State.GroupSize==0&&
                        game.State.Pieces[truck.cargo.Last()].color==4&&
                        truck.cargo.Skip(truck.cargo.Count-game.State.GroupSize).All(id=>game.State.Pieces[id].location==PieceLocation.Cargo))
                        Assert.IsTrue(game.TryRelease(index));
                }
                yield return null;
            }
            Assert.IsTrue(game.State.Carriers[2].curtainOpen,"Yellow cargo never completed a matching truck");
            yield return new WaitForSeconds(.6f);
            Assert.IsFalse(game.TruckViews[2].GetComponent<CurtainView>().IsCovering);
            Assert.IsTrue(game.TruckViews[2].Find("Cargo groups").gameObject.activeSelf);
            foreach(int index in new[]{3,4})
            {
                Assert.IsFalse(game.State.Carriers[index].curtainOpen);
                Assert.IsTrue(game.TruckViews[index].GetComponent<CurtainView>().IsCovering);
            }
            Assert.IsTrue(game.Undo());yield return null;
            Assert.AreEqual(count,game.State.Carriers.Count);Assert.IsFalse(game.State.Carriers[2].curtainOpen);
            Assert.IsTrue(game.TruckViews[2].GetComponent<CurtainView>().IsCovering);
            Assert.IsFalse(game.TruckViews[2].Find("Cargo groups").gameObject.activeSelf);
            Assert.Zero(game.State.PendingTransfers);game.State.AssertConservation();
        }
        [UnityTest] public IEnumerator EnlargedBeltPiecesKeepTheirSizeThroughPauseAndUndo()
        {
            game.LoadLevel(3);Assert.IsTrue(game.TryRelease(0));
            float deadline=Time.realtimeSinceStartup+8;
            while(game.State.PendingTransfers>0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.Zero(game.State.PendingTransfers);
            var id=game.State.Pieces.First(p=>p.location==PieceLocation.Belt).id;var view=game.PieceViews[id];
            float size=view.GetComponent<MeshFilter>().sharedMesh.bounds.size.x*view.transform.lossyScale.x;
            Assert.GreaterOrEqual(size,.459f);Assert.Greater(size,game.State.Fragments.CellSize*1.4f);
            Assert.Greater(view.GetComponent<SphereCollider>().radius*2*view.transform.lossyScale.x,.42f,"Collisions must grow with the visible cube");
            game.SetPaused(true);yield return new WaitForSeconds(.1f);
            Assert.AreEqual(size,view.GetComponent<MeshFilter>().sharedMesh.bounds.size.x*view.transform.lossyScale.x,.001f);game.SetPaused(false);
            Assert.IsTrue(game.TryRelease(1));yield return new WaitForSeconds(.1f);Assert.IsTrue(game.Undo());
            Assert.AreEqual(PieceLocation.Belt,game.State.Pieces[id].location);
            Assert.AreEqual(size,view.GetComponent<MeshFilter>().sharedMesh.bounds.size.x*view.transform.lossyScale.x,.001f);
            Assert.IsTrue(view.Body.detectCollisions);game.State.AssertConservation();
        }
        [Test] public void ResultDialogCoversTheCompletedBadgeInTheRenderedImage()
        {
            game.LoadLevel(1);var badge=game.TruckViews[1].Find("Completed check").gameObject;
            badge.transform.position=game.Camera.ViewportToWorldPoint(new Vector3(.5f,.53f,30));
            badge.SetActive(true);var visible=RenderPixels(game.Camera);badge.SetActive(false);var without=RenderPixels(game.Camera);
            Assert.Greater(visible.Where((pixel,i)=>!pixel.Equals(without[i])).Count(),5,"The test badge must actually be visible");
            game.Hud.Result(true);badge.SetActive(true);var covered=RenderPixels(game.Camera);
            badge.SetActive(false);var hidden=RenderPixels(game.Camera);
            Assert.Zero(covered.Where((pixel,i)=>!pixel.Equals(hidden[i])).Count(),"A completed badge rendered above the result dialog");
        }
        static Color32[] RenderPixels(Camera camera)
        {
            var previous=camera.targetTexture;var old=RenderTexture.active;var rt=new RenderTexture(160,284,24);rt.Create();
            var texture=new Texture2D(160,284,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,160,284),0,0);texture.Apply();return texture.GetPixels32();
            }
            finally
            {
                camera.targetTexture=previous;RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);
            }
        }
        [Test] public void ChineseHudHasGlyphsAndAVisibleLevelButton()
        {
            game.LoadLevel(1);
            var button=game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="Level Select");
            Assert.IsNotNull(button.GetComponent<UnityEngine.UI.Image>().sprite);Assert.Greater(button.GetComponent<UnityEngine.UI.Image>().color.a,.9f);
            foreach(var show in new System.Action[]{()=>game.Hud.Close(),()=>game.Hud.Pause(game.Progress),()=>game.Hud.Result(true),()=>game.Hud.Result(false),()=>game.Hud.Levels(game.Catalog,1)})
            {
                show();
                foreach(var text in game.Hud.GetComponentsInChildren<UnityEngine.UI.Text>())
                    foreach(char character in text.text)
                    {
                        Assert.IsFalse(character>='A'&&character<='Z'||character>='a'&&character<='z',"English UI: "+text.text);
                        if(!char.IsWhiteSpace(character))Assert.IsTrue(text.font.HasCharacter(character),"Missing Chinese glyph: "+character);
                    }
            }
        }
        [Test] public void ExistingSaveGetsFiveHundredThousandCoinsOnlyOnce()
        {
            var progress=JsonUtility.FromJson<PlayerProgress>("{\"version\":1,\"currentLevel\":25,\"highestUnlocked\":30,\"coins\":123,\"undo\":1}");
            Assert.IsTrue(progress.GrantStartingCoins());Assert.AreEqual(500000,progress.coins);
            Assert.AreEqual(25,progress.currentLevel);Assert.AreEqual(30,progress.highestUnlocked);Assert.AreEqual(1,progress.undo);
            progress.coins-=900;var restored=JsonUtility.FromJson<PlayerProgress>(JsonUtility.ToJson(progress));
            Assert.IsFalse(restored.GrantStartingCoins());Assert.AreEqual(499100,restored.coins);
        }
        // Removed the artificial-overpass regressions: native level 18 is an open spline,
        // with end-portal transport, rather than the previously invented two-deck loop.
    }
}

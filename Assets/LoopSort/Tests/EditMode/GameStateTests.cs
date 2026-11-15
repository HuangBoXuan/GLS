using System;
using System.Linq;
using NUnit.Framework;
using LoopSort0.Core;

namespace LoopSort0.Tests
{
    public sealed class GameStateTests
    {
        static LevelData Level(int slots=7,params int[][] stacks)
        {
            if(stacks.Length==0)stacks=new[]{new[]{1,1,1},new[]{1}};
            return new LevelData{id=1,slots=slots,unsupported=Array.Empty<string>(),carriers=stacks.Select((colors,i)=>new CarrierData{
                id=((char)('A'+i)).ToString(),groups=colors.Select(c=>new CargoGroup{color=c}).ToArray()}).ToArray()};
        }
        static void Land(GameState state,int[] pieces) {foreach(int id in pieces)Assert.IsTrue(state.EnterBelt(id,state.Epoch));}
        static int[] ReleaseAndLandGroups(GameState state,int carrier,int groups)
        {
            var move=state.Release(carrier);Assert.IsTrue(move.accepted,move.reason);
            Assert.AreEqual(groups*state.GroupSize,move.pieces.Length);Land(state,move.pieces);return move.pieces;
        }
        [Test] public void SourceGroupContainsEighteenCubes()
        {
            var game=new GameState(Level());Assert.AreEqual(72,game.TotalPieces);Assert.AreEqual(54,game.Carriers[0].cargo.Count);game.AssertConservation();
        }
        [Test] public void ReleasesAllWholeOuterBlocksWithTheSameColor()
        {
            var game=new GameState(Level(7,new[]{1,2,2}));var move=game.Release(0);
            Assert.IsTrue(move.accepted);Assert.AreEqual(36,move.pieces.Length);Assert.AreEqual(18,game.Carriers[0].cargo.Count);
            CollectionAssert.AreEquivalent(Enumerable.Range(18,36),move.pieces);
            Assert.IsTrue(move.pieces.All(i=>game.Pieces[i].color==2));game.AssertConservation();
        }
        [Test] public void ReservingReleasePreventsConveyorOverbooking()
        {
            var game=new GameState(Level(1,new[]{1},new[]{2}));Assert.IsTrue(game.Release(0).accepted);
            Assert.IsFalse(game.Release(1).accepted);Assert.AreEqual(18,game.BeltCount);game.AssertConservation();
        }
        [Test] public void TwoFreeSlotsReleaseTwoOfThreeMatchingBlocks()
        {
            var state=new GameState(Level(2,new[]{1,2,2,2}));var move=state.Release(0);
            Assert.IsTrue(move.accepted);Assert.AreEqual(2*state.GroupSize,move.pieces.Length);
            CollectionAssert.AreEquivalent(Enumerable.Range(36,36),move.pieces);
            Assert.AreEqual(2*state.GroupSize,state.Carriers[0].cargo.Count);
            Assert.AreEqual(state.BeltCapacity,state.BeltCount);state.AssertConservation();
        }
        [Test] public void DuplicateReleaseCannotCreateMoreCubes()
        {
            var game=new GameState(Level());var first=game.Release(0);Assert.IsFalse(game.Release(0).accepted);
            Assert.AreEqual(54,game.BeltCount);Assert.AreEqual(72,game.TotalPieces);
        }
        [Test] public void MismatchedColorCannotEnterTruck()
        {
            var game=new GameState(Level(7,new[]{1},new[]{2}));var move=game.Release(0);Land(game,move.pieces);
            Assert.IsFalse(game.ReserveReceive(move.pieces[0],1));game.AssertConservation();
        }
        [Test] public void IntakeReservationsCannotExceedTruckCapacity()
        {
            var game=new GameState(Level(7,new[]{1,1,1,1},new[]{1}));var ids=ReleaseAndLandGroups(game,0,4);
            foreach(int id in ids.Take(54))Assert.IsTrue(game.ReserveReceive(id,1));
            Assert.IsFalse(game.ReserveReceive(ids[54],1));Assert.AreEqual(72,game.Carriers[1].cargo.Count);game.AssertConservation();
        }
        [Test] public void SortingAllCubesWinsOnlyAfterLastArrival()
        {
            var game=new GameState(Level());var ids=ReleaseAndLandGroups(game,0,3);
            foreach(int id in ids)Assert.IsTrue(game.ReserveReceive(id,1));
            foreach(int id in ids.Take(ids.Length-1))game.FinishReceive(id,game.Epoch);
            Assert.AreEqual(RunStatus.Playing,game.Status);game.FinishReceive(ids.Last(),game.Epoch);
            Assert.AreEqual(RunStatus.Won,game.Status);Assert.AreEqual(72,game.Pieces.Count(p=>p.location==PieceLocation.Packed));
        }
        [Test] public void FullConveyorWithLegalReceiverDoesNotFail()
        {
            var game=new GameState(Level(1,new[]{1},Array.Empty<int>()));var move=game.Release(0);Land(game,move.pieces);game.Evaluate();
            Assert.AreEqual(RunStatus.Playing,game.Status);
        }
        [Test] public void MovingCubesCannotCausePrematureFailure()
        {
            var game=new GameState(Level(1,new[]{1}));game.Release(0);game.Evaluate();Assert.AreEqual(RunStatus.Playing,game.Status);
        }
        [Test] public void UndoInvalidatesLateTransferCallbacks()
        {
            var game=new GameState(Level());var snapshot=game.Capture();var move=game.Release(0);int epoch=game.Epoch;
            game.Restore(snapshot);Assert.IsFalse(game.EnterBelt(move.pieces[0],epoch));Assert.AreEqual(0,game.BeltCount);game.AssertConservation();
        }
        [Test] public void SnapshotIsIndependentOfFutureMoves()
        {
            var game=new GameState(Level());var snapshot=game.Capture();game.Release(0);
            Assert.AreEqual(54,snapshot.carriers[0].cargo.Count);Assert.IsTrue(snapshot.pieces.All(p=>p.location==PieceLocation.Cargo));
        }
        [Test] public void ShufflePreservesIdsColorsAndCounts()
        {
            var game=new GameState(Level(7,new[]{1,2,3,4}));var before=game.Carriers[0].cargo.ToArray();
            Assert.IsTrue(game.Shuffle(0,new Random(7)));CollectionAssert.AreEquivalent(before,game.Carriers[0].cargo);
            Assert.IsFalse(before.SequenceEqual(game.Carriers[0].cargo));game.AssertConservation();
        }
        [Test] public void ExpandingConveyorAddsOneSourceGroup()
        {
            var game=new GameState(Level());int before=game.BeltCapacity;game.ExpandBelt();Assert.AreEqual(before+18,game.BeltCapacity);
        }
        [Test] public void UnimplementedMechanicCannotSilentlyRunAsNormalLevel()
        {
            var level=Level();level.unsupported=new[]{"carrier:Ice"};Assert.Throws<NotSupportedException>(()=>new GameState(level));
        }
        [Test] public void ExplicitFallbackOpensSpecialLevelWithoutErasingItsFeatureMetadata()
        {
            var level=Level();level.unsupported=new[]{"carrier:Ice"};
            var state=new GameState(level,allowBasicRulesFallback:true);
            Assert.AreEqual(72,state.TotalPieces);Assert.IsFalse(level.IsSupported);
            CollectionAssert.AreEqual(new[]{"carrier:Ice"},level.unsupported);state.AssertConservation();
        }
        [Test] public void SameTruckWaitsUntilTheLastFragmentLandsBeforeReleasingAgain()
        {
            var state=new GameState(Level(4,new[]{1,2}));
            var first=state.Release(0);Assert.IsTrue(first.accepted);Assert.IsFalse(state.Release(0).accepted);
            Land(state,first.pieces.Take(first.pieces.Length-1).ToArray());
            Assert.AreEqual(1,state.Carriers[0].releasing);Assert.IsFalse(state.Release(0).accepted);
            Land(state,new[]{first.pieces.Last()});Assert.Zero(state.Carriers[0].releasing);
            var second=state.Release(0);Assert.IsTrue(second.accepted);Assert.AreEqual(state.GroupSize,second.pieces.Length);
            Assert.AreEqual(36,first.pieces.Concat(second.pieces).Distinct().Count());state.AssertConservation();
        }
        [Test] public void DifferentTrucksCanReleaseWholeBlocksConcurrently()
        {
            var state=new GameState(Level(4,new[]{1,2},new[]{2,1}));
            Assert.IsTrue(state.Release(0).accepted);Assert.IsTrue(state.Release(1).accepted);
            Assert.AreEqual(state.GroupSize,state.Carriers[0].releasing);
            Assert.AreEqual(state.GroupSize,state.Carriers[1].releasing);state.AssertConservation();
        }
        [TestCase(0)][TestCase(1)][TestCase(17)]
        public void ClickCancelsPartialIntakeAndReleasesBothLandedAndIncomingFragments(int landed)
        {
            var state=new GameState(Level(7,new[]{1},Array.Empty<int>()));var source=state.Release(0);Land(state,source.pieces);
            for(int i=0;i<17;i++)
            {
                Assert.IsTrue(state.ReserveReceive(source.pieces[i],1));
                if(i<landed)Assert.IsTrue(state.FinishReceive(source.pieces[i],state.Epoch));
            }
            var before=state.Carriers[1].cargo.ToArray();
            var outgoing=state.Release(1);Assert.IsTrue(outgoing.accepted);
            CollectionAssert.AreEquivalent(before,outgoing.pieces);Assert.Zero(state.Carriers[1].cargo.Count);
            Assert.AreEqual(17-landed,outgoing.interruptedReceives.Length);
            foreach(int id in outgoing.interruptedReceives)Assert.IsFalse(state.FinishReceive(id,state.Epoch));
            Assert.AreEqual(17,state.Carriers[1].releasing);state.AssertConservation();
        }
        [Test] public void FullyReservedBlockCanReverseBeforeTheLastFragmentLands()
        {
            var state=new GameState(Level(7,new[]{1},Array.Empty<int>()));var source=state.Release(0);Land(state,source.pieces);
            foreach(int id in source.pieces)Assert.IsTrue(state.ReserveReceive(id,1));
            foreach(int id in source.pieces.Take(source.pieces.Length-1))Assert.IsTrue(state.FinishReceive(id,state.Epoch));
            var outgoing=state.Release(1);Assert.IsTrue(outgoing.accepted);
            CollectionAssert.AreEquivalent(source.pieces,outgoing.pieces);
            CollectionAssert.AreEqual(new[]{source.pieces.Last()},outgoing.interruptedReceives);
            Assert.IsFalse(state.FinishReceive(source.pieces.Last(),state.Epoch));
            Assert.Zero(state.Carriers[1].cargo.Count);state.AssertConservation();
        }
        [Test] public void FullBeltLeavesIntakeReservationsUnchangedWhenCancellationCannotFit()
        {
            var state=new GameState(Level(2,new[]{1,1},Array.Empty<int>(),new[]{2}));
            var incoming=state.Release(0);Land(state,incoming.pieces);
            var returning=incoming.pieces.Take(state.GroupSize).ToArray();
            foreach(int id in returning)Assert.IsTrue(state.ReserveReceive(id,1));
            Assert.IsTrue(state.Release(2).accepted);Assert.AreEqual(state.BeltCapacity,state.BeltCount);
            Assert.IsFalse(state.Release(1).accepted);CollectionAssert.AreEqual(returning,state.Carriers[1].cargo);
            Assert.IsTrue(returning.All(id=>state.Pieces[id].location==PieceLocation.Receiving));state.AssertConservation();
        }
        [Test] public void InsufficientSpaceRejectsTheWholeBlockWithoutReleasingAnyFragments()
        {
            var state=new GameState(Level(1,new[]{1},new[]{2},Array.Empty<int>()));
            var first=state.Release(0);Land(state,first.pieces);
            Assert.IsTrue(state.ReserveReceive(first.pieces[0],2));Assert.IsTrue(state.FinishReceive(first.pieces[0],state.Epoch));
            var before=state.Carriers[1].cargo.ToArray();Assert.IsFalse(state.Release(1).accepted);
            CollectionAssert.AreEqual(before,state.Carriers[1].cargo);Assert.Zero(state.Carriers[1].releasing);
            state.AssertConservation();
        }
        [Test] public void SourceSizedLoopSelectsFortyFragmentsPerGroupInsteadOfEighteen()
        {
            var level=Level();level.points=new[]{new TrackPoint{x=.58f,z=2.9f},new TrackPoint{x=.58f,z=12.76f},new TrackPoint{x=9.28f,z=12.76f},new TrackPoint{x=9.28f,z=2.9f}};
            var state=new GameState(level);Assert.AreEqual(40,state.GroupSize);Assert.AreEqual(160,state.TotalPieces);
            Assert.AreEqual(160,state.TruckCapacity);Assert.AreEqual(120,state.Release(0).pieces.Length);state.AssertConservation();
        }
        [Test] public void MatchingTruckCompletionOpensOnlyTheMatchingCurtainAndUndoRestoresIt()
        {
            var level=Level(7,new[]{1,1,1},new[]{1},new[]{2},new[]{3});
            level.carriers[2].curtainColor=1;level.carriers[3].curtainColor=2;
            var state=new GameState(level);var snapshot=state.Capture();
            Assert.IsFalse(state.Release(2).accepted);Assert.IsFalse(state.CanReceive(2,2));
            var ids=ReleaseAndLandGroups(state,0,3);
            foreach(int id in ids)Assert.IsTrue(state.ReserveReceive(id,1));
            foreach(int id in ids.Take(ids.Length-1))state.FinishReceive(id,state.Epoch);
            Assert.IsFalse(state.Carriers[2].curtainOpen);
            state.FinishReceive(ids.Last(),state.Epoch);
            Assert.IsTrue(state.Carriers[2].curtainOpen);Assert.IsFalse(state.Carriers[3].curtainOpen);
            Assert.IsTrue(state.Release(2).accepted);state.AssertConservation();
            state.Restore(snapshot);Assert.IsFalse(state.Carriers[2].curtainOpen);state.AssertConservation();
        }
    }
}

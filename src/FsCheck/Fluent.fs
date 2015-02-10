﻿(*--------------------------------------------------------------------------*\
**  FsCheck                                                                 **
**  Copyright (c) 2008-2015 Kurt Schelfthout and contributors.              **  
**  All rights reserved.                                                    **
**  https://github.com/kurtschelfthout/FsCheck                              **
**                                                                          **
**  This software is released under the terms of the Revised BSD License.   **
**  See the file License.txt for the full text.                             **
\*--------------------------------------------------------------------------*)

namespace FsCheck

open System
open System.Linq
open System.ComponentModel
open System.Collections.Generic
open FsCheck
open Common
open Arb
open Gen
open Testable
open Prop
open Runner

//TODO:
//Within -> rely on testing frameworks?
//Throws -> rely on testing frameworks?
//"And" and "Or" should start a new property, with own classifies and labels etc (see prop_Label)
//label: maybe add some overloads, should be able to nest (see propMul)

///2-tuple containing a weight and a value, used in some Gen methods to indicate
///the probability of a value.
[<NoComparison>]
type WeightAndValue<'a> =
    { Weight: int
      Value : 'a  
    }
 
///Methods to build random value generators.
[<AbstractClass; Sealed>]
type Gen private() =
    static let regs = Runner.init.Force()

    ///Always generate value.
    ///[category: Creating generators] 
    static member Constant (value) = 
        constant value

    ///Build a generator that randomly generates one of the values in the given non-empty IEnumerable.
    ///[category: Creating generators]
    static member Elements (values : seq<_>) = 
        values |> elements

    ///Build a generator that randomly generates one of the given values.
    ///[category: Creating generators]
    static member Elements ([<ParamArrayAttribute>] values : array<_>) = 
        values |> elements

    ///Generates an integer between l and h, inclusive.
    ///[category: Creating generators]
    static member Choose (l,h) = 
        choose (l,h)

    ///Build a generator that generates a value from one of the given generators, with
    ///equal probability.
    ///[category: Creating generators from generators]
    static member OneOf (generators : seq<Gen<_>>) = 
        generators |> oneof

    ///Build a generator that generates a value from one of the given generators, with
    ///equal probability.
    ///[category: Creating generators from generators]
    static member OneOf ([<ParamArrayAttribute>]  generators : array<Gen<_>>) = 
        generators |> oneof

    ///Build a generator that generates a value from one of the generators in the given non-empty seq, with
    ///given probabilities. The sum of the probabilities must be larger than zero.
    ///[category: Creating generators from generators]
    static member Frequency ( weighedValues : seq<WeightAndValue<Gen<'a>>> ) =
        weighedValues |> Gen.FrequencyOfWeighedSeq

    ///Build a generator that generates a value from one of the generators in the given non-empty seq, with
    ///given probabilities. The sum of the probabilities must be larger than zero.
    ///[category: Creating generators from generators]
    static member Frequency ( [<ParamArrayAttribute>] weighedValues : array<WeightAndValue<Gen<'a>>> ) =
        weighedValues |> Gen.FrequencyOfWeighedSeq

    static member private FrequencyOfWeighedSeq ws = 
        ws |> Seq.map (fun wv -> (wv.Weight, wv.Value)) |> frequency

    ///Sequence the given list of generators into a generator of a list.
    ///[category: Creating generators from generators]
    static member Sequence<'a> (generators:seq<Gen<'a>>) = 
        generators |> sequence |> map (fun list -> list :> IEnumerable<'a>)

    ///Sequence the given list of generators into a generator of a list.
    ///[category: Creating generators from generators]
    static member Sequence<'a> ([<ParamArrayAttribute>]generators:array<Gen<'a>>) = 
        generators |> sequence |> map (fun list -> list.ToArray())

    /// Generates sublists of the given IEnumerable.
    ///[category: Creating generators]
    static member SubListOf s = 
        subListOf s
        |> map (fun l -> new List<_>(l) :> IList<_>)

    /// Generates sublists of the given arguments.
    ///[category: Creating generators]
    static member SubListOf ([<ParamArrayAttribute>] s:_ array) = 
        subListOf s
        |> map (fun l -> new List<_>(l) :> IList<_>)

    ///Obtain the current size. sized g calls g, passing it the current size as a parameter.
    ///[category: Managing size]
    static member Sized (sizedGen : Func<int,Gen<_>>) =
        sized <| fun s -> (sizedGen.Invoke(s))

///Methods to build shrinkers.
type Shrink =
    ///Returns the immediate shrinks for the given value based on its type.
    static member Type<'a>() = shrink<'a>

///Configure the test run.
type Configuration() =
    let mutable maxTest = Config.Quick.MaxTest
    let mutable maxFail = Config.Quick.MaxFail
    let mutable name = Config.Quick.Name
    let mutable every = Config.Quick.Every
    let mutable everyShrink = Config.Quick.EveryShrink
    let mutable startSize = Config.Quick.StartSize
    let mutable endSize = Config.Quick.EndSize
    let mutable runner = Config.Quick.Runner
    let mutable replay = Config.Quick.Replay

    ///The maximum number of tests that are run.
    member x.MaxNbOfTest with get() = maxTest and set(v) = maxTest <- v

    ///The maximum number of tests where values are rejected
    member x.MaxNbOfFailedTests with get() = maxFail and set(v) = maxFail <- v

    ///Name of the test.
    member x.Name with get() = name and set(v) = name <- v

    ///What to print when new arguments args are generated in test n
    member x.Every with get() = new Func<int,obj array,string>(fun i arr -> every i (Array.toList arr)) 
                   and set(v:Func<int,obj array,string>) = every <- fun i os -> v.Invoke(i,List.toArray os)

    ///What to print every time a counter-example is succesfully shrunk
    member x.EveryShrink with get() = new Func<obj array,string>(Array.toList >> everyShrink)
                         and set(v:Func<obj array,string>) = everyShrink <- fun os -> v.Invoke(List.toArray os)

    ///The size to use for the first test.
    member x.StartSize with get() = startSize and set(v) = startSize <- v

    ///The size to use for the last test, when all the tests are passing. The size increases linearly between Start- and EndSize.
    member x.EndSize with get() = endSize and set(v) = endSize <- v

    ///A custom test runner, e.g. to integrate with a test framework like xUnit or NUnit. 
    member x.Runner with get() = runner and set(v) = runner <- v

    //TODO: figure out how to deal with null values
    //member x.Replay with get() = (match replay with None -> null | Some s -> s) and set(v) = replay = Some v
    member internal x.ToConfig() =
        { MaxTest = maxTest
          MaxFail = maxFail 
          Name = name
          Every = every
          EveryShrink = everyShrink
          StartSize = startSize
          EndSize = endSize
          Runner = runner
          Replay = None
          Arbitrary = []
        }

///Specify a property to test.
[<AbstractClass>]
type Specification() =
    inherit obj()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    override x.Equals(other) = base.Equals(other)
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    override x.GetHashCode() = base.GetHashCode()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    override x.ToString() = base.ToString()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    abstract Build : unit -> Property

    /// Check one property with the quick configuration
    member x.QuickCheck() = Check.Quick(x.Build())

    /// Check one property with the quick configuration, and throw an exception if it fails or is exhausted.
    member x.QuickCheckThrowOnFailure() = Check.QuickThrowOnFailure(x.Build())

    /// Check one property with the verbose configuration.
    member x.VerboseCheck() = Check.Verbose(x.Build())

    /// Check one property with the quick configuration, and using the given name.
    member x.QuickCheck(name:string) = Check.Quick(name,x.Build())

    /// Check one property with the quick configuration, and throw an exception if it fails or is exhausted.
    member x.QuickCheckThrowOnFailure(name:string) = Check.QuickThrowOnFailure(name,x.Build())

    ///Check one property with the verbose configuration, and using the given name.
    member x.VerboseCheck(name:string) = Check.Verbose(name,x.Build())

    ///Check the given property using the given config.
    member x.Check(configuration:Configuration) = Check.One(configuration.ToConfig(),x.Build())

and SpecBuilder<'a> internal   ( generator0:'a Gen
                               , shrinker0: 'a -> 'a seq
                               , assertion0:'a -> Property
                               , conditions:('a -> bool) list
                               , collects:('a -> string) list
                               , classifies:(('a -> bool) * string) list) =
    inherit Specification()
    override x.Build() =
            let conditions' a = conditions |> List.fold (fun s f -> s && f a) true
            let collects' a prop = collects |> List.fold (fun prop f -> prop |> collect (f a)) prop
            let classifies' a prop = classifies |> List.fold (fun prop (f,name) -> prop |> classify (f a) name) prop  
            forAll (Arb.fromGenShrink(generator0,shrinker0)) (fun a -> (conditions' a) ==> lazy (assertion0 a) |> collects' a |> classifies' a)
    member x.When( condition:Func<'a,bool> ) = 
        SpecBuilder<'a>(generator0, shrinker0, assertion0, (fun a -> condition.Invoke(a))::conditions, collects, classifies)

    ///Collect data values. The argument of collect is evaluated in each test case, 
    ///and the distribution of values is reported.
    member x.Collect(collectedValue:Func<'a,string>)=
        SpecBuilder<'a>(generator0, shrinker0,assertion0,conditions,(fun a -> collectedValue.Invoke(a))::collects,classifies)

    member x.Classify(filter:Func<'a,bool>,name:string) =
        SpecBuilder<'a>(generator0, shrinker0,assertion0,conditions,collects,((fun a -> filter.Invoke(a)),name)::classifies)
    member x.Shrink(shrinker:Func<'a,'a seq>) =
        SpecBuilder<'a>( generator0, shrinker.Invoke, assertion0, conditions, collects, classifies)
    member x.Label( name:string ) =
        SpecBuilder<'a>(generator0, shrinker0, label name << assertion0,conditions, collects, classifies)

    ///Construct a property that succeeds if both succeed.
    member x.And(assertion : Func<'a,bool>) =
        SpecBuilder<'a>( generator0, shrinker0, (fun a -> (assertion0 a) .&. (assertion.Invoke(a))), conditions, collects, classifies)

    ///Construct a property that succeeds if both succeed.
    member x.And(assertion : Func<'a,bool>, name:string ) =
        SpecBuilder<'a>( generator0, shrinker0, (fun a -> (assertion0 a) .&. (label name (assertion.Invoke(a)))), conditions, collects, classifies)

    ///Construct a property that fails if both fail.
    member x.Or(assertion : Func<'a,bool>) =
        SpecBuilder<'a>( generator0, shrinker0, (fun a -> (assertion0 a) .|. (assertion.Invoke(a))), conditions, collects, classifies)

    ///Construct a property that fails if both fail.
    member x.Or(assertion : Func<'a,bool>, name:string ) =
        SpecBuilder<'a>( generator0, shrinker0, (fun a -> (assertion0 a) .|. (label name (assertion.Invoke(a)))), conditions, collects, classifies)
    member x.AndFor<'b>(generator:'b Gen, assertion:Func<'b,bool>) =
        SpecBuilder<'a,'b>  (generator0
                            ,shrinker0 
                            ,generator
                            ,shrink
                            ,fun a b -> (assertion0 a) .&. property (assertion.Invoke(b))
                            ,conditions |> List.map (fun f -> (fun a b -> f a))
                            ,collects |> List.map (fun f -> (fun a b -> f a))
                            ,classifies |> List.map (fun (f,name) -> ((fun a b -> f a),name))
                            )
  
       
and SpecBuilder<'a,'b> internal   ( generator0:'a Gen
                                  , shrinker0: 'a -> 'a seq
                                  , generator1:'b Gen
                                  , shrinker1: 'b -> 'b seq
                                  , assertion0:'a -> 'b -> Property
                                  , conditions:('a -> 'b -> bool) list
                                  , collects:('a -> 'b -> string) list
                                  , classifies:(('a -> 'b -> bool) * string) list) = 
    inherit Specification()
    override x.Build() =
            let conditions' a b = conditions |> List.fold (fun s f -> s && f a b) true
            let collects' a b prop = collects |> List.fold (fun prop f -> prop |> collect (f a b)) prop
            let classifies' a b prop = classifies |> List.fold (fun prop (f,name) -> prop |> classify (f a b) name) prop  
            forAll (Arb.fromGen generator0) (fun a -> forAll (Arb.fromGen generator1) (fun b -> (conditions' a b) ==> lazy (assertion0 a b) |> collects' a b |> classifies' a b))
    member x.When( condition:Func<'a,'b,bool> ) = 
        SpecBuilder<'a,'b>(generator0, shrinker0, generator1, shrinker1, assertion0, (fun a b -> condition.Invoke(a,b))::conditions, collects, classifies)
    member x.Collect(collectedValue:Func<'a,'b,string>)=
        SpecBuilder<'a,'b>(generator0, shrinker0, generator1, shrinker1, assertion0,conditions,(fun a b -> collectedValue.Invoke(a,b))::collects,classifies)
    member x.Classify(filter:Func<'a,'b,bool>,name:string) =
        SpecBuilder<'a,'b>(generator0, shrinker0,generator1, shrinker1,assertion0,conditions,collects,((fun a b -> filter.Invoke(a,b)),name)::classifies)
    member x.Shrink(shrinker:Func<'b,'b seq>) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker.Invoke, assertion0, conditions, collects, classifies)
    member x.Label( name:string ) =
        SpecBuilder<'a,'b>(generator0, shrinker0, generator1, shrinker1, (fun a b-> label name (assertion0 a b)),conditions, collects, classifies)
    member x.And(assertion : Func<'a,'b,bool>) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker1,
            (fun a b -> (assertion0 a b) .&. (assertion.Invoke(a, b))) , conditions, collects, classifies)
    member x.And(assertion : Func<'a,'b,bool>, name:string ) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker1, 
            (fun a b -> (assertion0 a b) .&. (label name (assertion.Invoke(a,b)))), conditions, collects, classifies)
    member x.Or(assertion : Func<'a,'b,bool>) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker1, 
            (fun a b -> (assertion0 a b) .|. (assertion.Invoke(a,b))), conditions, collects, classifies)
    member x.Or(assertion : Func<'a,'b,bool>, name:string ) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker1, 
            (fun a b-> (assertion0 a b) .|. (label name (assertion.Invoke(a,b)))), conditions, collects, classifies)
    member x.AndFor<'c>(generator:'c Gen, assertion:Func<'c,bool>) =
        SpecBuilder<'a,'b,'c>   (generator0, shrinker0
                                ,generator1, shrinker1
                                ,generator, shrink
                                ,fun a b c -> (assertion0 a b) .&. property (assertion.Invoke(c))
                                ,conditions |> List.map (fun f -> (fun a b c -> f a b))
                                ,collects |> List.map (fun f -> (fun a b c -> f a b))
                                ,classifies |> List.map (fun (f,name) -> (fun a b c -> f a b),name)
                                )
                                
and SpecBuilder<'a,'b,'c> internal  ( generator0:'a Gen
                                    , shrinker0:'a -> 'a seq
                                    , generator1:'b Gen
                                    , shrinker1: 'b -> 'b seq
                                    , generator2:'c Gen
                                    , shrinker2: 'c -> 'c seq
                                    , assertion0:'a -> 'b -> 'c -> Property
                                    , conditions:('a -> 'b -> 'c -> bool) list
                                    , collects:('a -> 'b -> 'c -> string) list
                                    , classifies:(('a -> 'b -> 'c -> bool) * string) list) = 
    inherit Specification()
    override x.Build() =
            let conditions' a b c = conditions |> List.fold (fun s f -> s && f a b c) true
            let collects' a b c prop = collects |> List.fold (fun prop f -> prop |> collect (f a b c)) prop
            let classifies' a b c prop = classifies |> List.fold (fun prop (f,name) -> prop |> classify (f a b c) name) prop  
            forAll (Arb.fromGen generator0) (fun a -> 
            forAll (Arb.fromGen generator1) (fun b -> 
            forAll (Arb.fromGen generator2) (fun c ->
                (conditions' a b c) ==> lazy (assertion0 a b c) |> collects' a b c |> classifies' a b c))) 
    member x.When( condition:Func<'a,'b,'c,bool> ) = 
        SpecBuilder<'a,'b,'c>(generator0, shrinker0, generator1, shrinker1, generator2, shrinker2, assertion0, (fun a b c -> condition.Invoke(a,b,c))::conditions, collects, classifies)
    member x.Collect(collectedValue:Func<'a,'b,'c,string>)=
        SpecBuilder<'a,'b,'c>(generator0, shrinker0, generator1, shrinker1, generator2, shrinker2, assertion0, conditions,(fun a b c -> collectedValue.Invoke(a,b,c))::collects,classifies)
    member x.Classify(filter:Func<'a,'b,'c,bool>,name:string) =
        SpecBuilder<'a,'b,'c>(generator0, shrinker0, generator1, shrinker1, generator2, shrinker2, assertion0, conditions, collects,((fun a b c -> filter.Invoke(a,b,c)),name)::classifies)         
    member x.Shrink(shrinker:Func<'c,'c seq>) =
        SpecBuilder<'a,'b,'c>(generator0, shrinker0, generator1, shrinker1, generator2, shrinker.Invoke, assertion0, conditions, collects, classifies)
    member x.Label( name:string ) =
        SpecBuilder<'a,'b,'c>(generator0, shrinker0, generator1, shrinker1, generator2, shrinker2, (fun a b c -> label name (assertion0 a b c)),conditions, collects, classifies)
    member x.And(assertion : Func<'a,'b,'c,bool>) =
        SpecBuilder<'a,'b,'c>( generator0, shrinker0, generator1, shrinker1,generator2, shrinker2,
            (fun a b c -> (assertion0 a b c) .&. (assertion.Invoke(a, b, c))) , conditions, collects, classifies)
    member x.And(assertion : Func<'a,'b,'c,bool>, name:string ) =
        SpecBuilder<'a,'b,'c>( generator0, shrinker0, generator1, shrinker1, generator2, shrinker2,
            (fun a b c -> (assertion0 a b c) .&. (label name (assertion.Invoke(a,b,c)))), conditions, collects, classifies)
    member x.Or(assertion : Func<'a,'b,'c,bool>) =
        SpecBuilder<'a,'b,'c>( generator0, shrinker0, generator1, shrinker1, generator2, shrinker2,
            (fun a b c -> (assertion0 a b c) .|. (assertion.Invoke(a,b,c))), conditions, collects, classifies)
    member x.Or(assertion : Func<'a,'b,'c,bool>, name:string ) =
        SpecBuilder<'a,'b,'c>( generator0, shrinker0, generator1, shrinker1,generator2, shrinker2, 
            (fun a b c -> (assertion0 a b c) .|. (label name (assertion.Invoke(a,b,c)))), conditions, collects, classifies)  
      
///Entry point to specifying a property.
type Spec private() =
    static let _ = Runner.init.Value
    static let noshrink = fun _ -> Seq.empty

    static member ForAny(assertion:Action<'a>) =
        Spec.For(Arb.from, assertion)
    static member ForAny(assertion:Func<'a,bool>) =
        Spec.For(Arb.from, assertion)
    
    static member ForAny(assertion:Action<'a,'b>) =
        Spec.For(Arb.from, Arb.from, assertion)
    static member ForAny(assertion:Func<'a,'b,bool>) =
        Spec.For(Arb.from, Arb.from, assertion)

    static member ForAny(assertion:Action<'a,'b,'c>) =
        Spec.For(Arb.from, Arb.from, Arb.from, assertion)
    static member ForAny(assertion:Func<'a,'b,'c,bool>) =
        Spec.For(Arb.from, Arb.from, Arb.from, assertion)
    
        
    static member For(generator:'a Gen, assertion:Func<'a,bool>) =
        SpecBuilder<'a>(generator, noshrink, property << assertion.Invoke, [], [], [])
    static member For(arbitrary:'a Arbitrary, assertion:Func<'a,bool>) =
        SpecBuilder<'a>(arbitrary.Generator, arbitrary.Shrinker, property << assertion.Invoke, [], [], [])

    static member For(generator:'a Gen, assertion:Action<'a>) =
        SpecBuilder<'a>(generator, noshrink, property << assertion.Invoke, [], [], [])
    static member For(arbitrary:'a Arbitrary, assertion:Action<'a>) =
        SpecBuilder<'a>(arbitrary.Generator, arbitrary.Shrinker, property << assertion.Invoke, [], [], [])

    static member For(generator1:'a Gen,generator2:'b Gen, assertion:Func<'a,'b,bool>) =
        SpecBuilder<'a,'b>(generator1, noshrink, generator2, noshrink, (fun a b -> property <| assertion.Invoke(a,b)),[],[],[])
    static member For(arbitrary1:'a Arbitrary,arbitrary2:'b Arbitrary, assertion:Func<'a,'b,bool>) =
        SpecBuilder<'a,'b>(arbitrary1.Generator, arbitrary1.Shrinker, arbitrary2.Generator, arbitrary2.Shrinker, (fun a b -> property <| assertion.Invoke(a,b)),[],[],[])

    static member For(generator1:'a Gen,generator2:'b Gen, assertion:Action<'a,'b>) =
        SpecBuilder<'a,'b>(generator1, noshrink, generator2, noshrink, (fun a b -> property <| assertion.Invoke(a,b)),[],[],[])
    static member For(arbitrary1:'a Arbitrary,arbitrary2:'b Arbitrary, assertion:Action<'a,'b>) =
        SpecBuilder<'a,'b>(arbitrary1.Generator, arbitrary1.Shrinker, arbitrary2.Generator, arbitrary2.Shrinker, (fun a b -> property <| assertion.Invoke(a,b)),[],[],[])

    static member For(generator1:'a Gen,generator2:'b Gen,generator3:'c Gen, assertion:Func<'a,'b,'c,bool>) =
        SpecBuilder<'a,'b,'c>(generator1, noshrink, generator2, noshrink, generator3, noshrink, (fun a b c -> property <| assertion.Invoke(a,b,c)),[],[],[])
    static member For(arbitrary1:'a Arbitrary,arbitrary2:'b Arbitrary,arbitrary3:'c Arbitrary, assertion:Func<'a,'b,'c,bool>) =
        SpecBuilder<'a,'b,'c>(arbitrary1.Generator, arbitrary1.Shrinker, arbitrary2.Generator, arbitrary2.Shrinker, arbitrary3.Generator, arbitrary3.Shrinker, (fun a b c -> property <| assertion.Invoke(a,b,c)),[],[],[])

    static member For(generator1:'a Gen,generator2:'b Gen,generator3:'c Gen, assertion:Action<'a,'b,'c>) =
        SpecBuilder<'a,'b,'c>(generator1, noshrink, generator2, noshrink, generator3, noshrink, (fun a b c -> property <| assertion.Invoke(a,b,c)),[],[],[])
    static member For(arbitrary1:'a Arbitrary,arbitrary2:'b Arbitrary,arbitrary3:'c Arbitrary, assertion:Action<'a,'b,'c>) =
        SpecBuilder<'a,'b,'c>(arbitrary1.Generator, arbitrary1.Shrinker, arbitrary2.Generator, arbitrary2.Shrinker, arbitrary3.Generator, arbitrary3.Shrinker, (fun a b c -> property <| assertion.Invoke(a,b,c)),[],[],[])


open Gen

///Extension methods to build generators - contains among other the Linq methods.
[<AbstractClass; Sealed; System.Runtime.CompilerServices.Extension>]
type GeneratorExtensions = 

    ///Generates a value with maximum size n.
    ///[category: Generating test values]
    [<System.Runtime.CompilerServices.Extension>]
    static member Eval(generator, size, random) =
        eval size random generator

    ///Generates n values of the given size.
    ///[category: Generating test values]
    [<System.Runtime.CompilerServices.Extension>]
    static member Sample(generator, size, numberOfSamples) =
        sample size numberOfSamples generator

    ///Map the given function to the value in the generator, yielding a new generator of the result type.  
    [<System.Runtime.CompilerServices.Extension>]
    static member Select(g:Gen<_>, selector : Func<_,_>) = g.Map(fun a -> selector.Invoke(a))

    ///Generates a value that satisfies a predicate. This function keeps re-trying
    ///by increasing the size of the original generator ad infinitum.  Make sure there is a high chance that 
    ///the predicate is satisfied.
    [<System.Runtime.CompilerServices.Extension>]
    static member Where(g:Gen<_>, predicate : Func<_,_>) = suchThat (fun a -> predicate.Invoke(a)) g
    
    [<System.Runtime.CompilerServices.Extension>]
    static member SelectMany(source:Gen<_>, f:Func<_, Gen<_>>) = 
        gen { let! a = source
              return! f.Invoke(a) }
    
    [<System.Runtime.CompilerServices.Extension>]
    static member SelectMany(source:Gen<_>, f:Func<_, Gen<_>>, select:Func<_,_,_>) =
        gen { let! a = source
              let! b = f.Invoke(a)
              return select.Invoke(a,b) }

    ///Generates a list of given length, containing values generated by the given generator.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member ListOf (generator, nbOfElements) =
        listOfLength nbOfElements generator
        |> map (fun l -> new List<_>(l) :> IList<_>)

    /// Generates a list of random length. The maximum length depends on the
    /// size parameter.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member ListOf (generator) =
        listOf generator
        |> map (fun l -> new List<_>(l) :> IList<_>)

    /// Generates a non-empty list of random length. The maximum length 
    /// depends on the size parameter.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member NonEmptyListOf<'a> (generator) = 
        nonEmptyListOf generator 
        |> map (fun list -> new List<'a>(list) :> IList<_>)
    
    /// Generates an array of a specified length.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member ArrayOf (generator, length) =
        arrayOfLength length generator

    /// Generates an array using the specified generator. 
    /// The maximum length is size+1.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member ArrayOf (generator) =
        arrayOf generator

    /// Generates a 2D array of the given dimensions.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member Array2DOf (generator, rows, cols) =
        array2DOfDim (rows,cols) generator

    /// Generates a 2D array. The square root of the size is the maximum number of rows and columns.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member Array2DOf (generator) =
        array2DOf generator

    ///Apply the given Gen function to this generator, aka the applicative <*> operator.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member Apply (generator, f:Gen<Func<_,_>>) =
        apply (f |> map (fun f -> f.Invoke)) generator

    ///Override the current size of the test.
    ///[category: Managing size]
    [<System.Runtime.CompilerServices.Extension>]
    static member Resize (generator, newSize) =
        resize newSize generator

    /// Construct an Arbitrary instance from a generator.
    /// Shrink is not supported for this type.
    [<System.Runtime.CompilerServices.Extension>]
    static member ToArbitrary generator =
        Arb.fromGen generator

    /// Construct an Arbitrary instance from a generator and a shrinker.
    [<System.Runtime.CompilerServices.Extension>]
    static member ToArbitrary (generator,shrinker) =
        Arb.fromGenShrink (generator,shrinker)

    ///Build a generator that generates a 2-tuple of the values generated by the given generator.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member Two (generator) =
        two generator

    ///Build a generator that generates a 3-tuple of the values generated by the given generator.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member Three (generator) =
        three generator

    ///Build a generator that generates a 4-tuple of the values generated by the given generator.
    ///[category: Creating generators from generators]
    [<System.Runtime.CompilerServices.Extension>]
    static member Four (generator) =
        four generator



///Extensons to transform Arbitrary instances into other Arbitrary instances.
[<System.Runtime.CompilerServices.Extension>]
type ArbitraryExtensions =
    ///Construct an Arbitrary instance for a type that can be mapped to and from another type (e.g. a wrapper),
    ///based on a Arbitrary instance for the source type and two mapping functions. 
    [<System.Runtime.CompilerServices.Extension>]
    static member Convert (arb, convertTo: Func<_,_>, convertFrom: Func<_,_>) =
        Arb.convert convertTo.Invoke convertFrom.Invoke arb

    /// Return an Arbitrary instance that is a filtered version of an existing arbitrary instance.
    /// The generator uses Gen.suchThat, and the shrinks are filtered using Seq.filter with the given predicate.
    [<System.Runtime.CompilerServices.Extension>]
    static member Filter (arb, filter: Func<_,_>) =
        Arb.filter filter.Invoke arb

    /// Return an Arbitrary instance that is a mapped and filtered version of an existing arbitrary instance.
    /// The generator uses Gen.map with the given mapper and then Gen.suchThat with the given predicate, 
    /// and the shrinks are filtered using Seq.filter with the given predicate.
    ///This is sometimes useful if using just a filter would reduce the chance of getting a good value
    ///from the generator - and you can map the value instead. E.g. PositiveInt.
    [<System.Runtime.CompilerServices.Extension>]
    static member MapFilter (arb, map: Func<_,_>, filter: Func<_,_>) =
        Arb.mapFilter map.Invoke filter.Invoke arb

  
///Register a number of Arbitrary instances so they are available implicitly.  
type DefaultArbitraries =
    ///Register the generators that are static members of the type argument.
    static member Add<'t>() = Arb.register<'t>()

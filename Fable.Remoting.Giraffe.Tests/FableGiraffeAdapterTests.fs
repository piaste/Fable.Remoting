﻿module FableGiraffeAdapterTests

open System
open System.Net
open System.Net.Http
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Giraffe.Middleware
open Giraffe.HttpHandlers
open Giraffe.Tasks
open Fable.Remoting.Giraffe
open System.Net.Http
open System
open Expecto
open Types

// Test helpers
FableGiraffeAdapter.logger <- Some (printfn "%s")
let equal x y = Expect.equal true (x = y) (sprintf "%A = %A" x y)
let pass () = Expect.equal true true ""   
let fail () = Expect.equal false true ""
let failUnexpect (x: obj) = Expect.equal false true (sprintf "%A was not expected" x) 
let giraffeApp : HttpHandler = FableGiraffeAdapter.httpHandlerFor implementation
let postContent (input: string) =  new StringContent(input, Text.Encoding.UTF8)
let configureApp (app : IApplicationBuilder) =
    app.UseGiraffe giraffeApp

let createHost() =
    WebHostBuilder()
        .UseContentRoot(Directory.GetCurrentDirectory())
        .Configure(Action<IApplicationBuilder> configureApp)

let postReq (path : string) (body: string) =
    let url = "http://127.0.0.1" + path
    let request = new HttpRequestMessage(HttpMethod.Post, url)
    request.Content <- postContent body
    request

let runTask task =
    task
    |> Async.AwaitTask
    |> Async.RunSynchronously

let testServer = new TestServer(createHost())
let client = testServer.CreateClient()

let makeRequest (request : HttpRequestMessage) =
    task {
        let! response = client.SendAsync request
        let! content = response.Content.ReadAsStringAsync()
        return content
    } |> runTask

let ofJson<'t> (input: string) = 
    FableGiraffeAdapter.deserialize<'t> input
let toJson (x: obj) = 
    FableGiraffeAdapter.json x

let fableGiraffeAdapterTests = 
    testList "FableGiraffeAdapter tests" [
        testCase "String round trip" <| fun () ->   
            let input = "\"hello\""
            let output = makeRequest (postReq "/IProtocol/echoString" (toJson input))
            match ofJson<string> output with
            | "\"hello\"" -> pass()
            | otherwise -> failUnexpect otherwise 

        testCase "Int round trip" <| fun () ->
            [-2; -1; 0; 1; 2]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoInteger" (toJson input)))
            |> List.map (fun output -> ofJson<int> output)
            |> function 
                | [-2; -1; 0; 1; 2] -> pass()
                | otherwise -> failUnexpect otherwise  

        testCase "Option<int> round trip" <| fun () ->
            [Some 2; None; Some -2]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoIntOption" (toJson input)))
            |> List.map (fun output -> ofJson<int option> output)
            |> function 
                | [Some 2; None; Some -2] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "Option<string> round trip" <| fun () ->
            [Some "hello"; None; Some "there"]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoStringOption" (toJson input)))
            |> List.map (fun output -> ofJson<string option> output)
            |> function 
                | [Some "hello"; None; Some "there"] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "Option<string> round trip" <| fun () ->
            [Some "hello"; None; Some "there"]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoStringOption" (toJson input)))
            |> List.map (fun output -> ofJson<string option> output)
            |> function 
                | [Some "hello"; None; Some "there"] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "bool round trip" <| fun () ->
            [true; false; true]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoBool" (toJson input)))
            |> List.map (fun output -> ofJson<bool> output)
            |> function 
                | [true; false; true] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "Generic union Maybe<int> round trip" <| fun () ->
            [Just 5; Nothing; Just -2]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoGenericUnionInt" (toJson input)))
            |> List.map (fun output -> ofJson<Maybe<int>> output)
            |> function 
                | [Just 5; Nothing; Just -2] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "Generic union Maybe<string> round trip" <| fun () ->
            [Just "hello"; Nothing; Just "there"; Just null]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoGenericUnionString" (toJson input)))
            |> List.map (fun output -> ofJson<Maybe<string>> output)
            |> function 
                | [Just "hello"; Nothing; Just "there"; Just null] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "Simple union round trip" <| fun () ->
            [A; B]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoSimpleUnion" (toJson input)))
            |> List.map (fun output -> ofJson<AB> output)
            |> function 
                | [A; B] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "List<int> round trip" <| fun () ->
            [[]; [1 .. 5]]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoIntList" (toJson input)))
            |> List.map (fun output -> ofJson<int list> output)
            |> function 
                | [[]; [1;2;3;4;5]] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "List<int> round trip" <| fun () ->
            [[1.5; 1.5; 3.0]]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/floatList" (toJson input)))
            |> List.map (fun output -> ofJson<float list> output)
            |> function 
                | [[1.5; 1.5; 3.0]] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "Unit as input with list result" <| fun () ->
            [(); ()]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/unitToInts" (toJson input)))
            |> List.map (fun output -> ofJson<int list> output)
            |> function 
                | [[1;2;3;4;5]; [1;2;3;4;5]] -> pass()
                | otherwise -> failUnexpect otherwise

        testCase "Record round trip" <| fun () ->
            [{ Prop1 = "hello"; Prop2 = 10; Prop3 = Some 5 }
             { Prop1 = "";      Prop2 = 1;  Prop3 = None }]
            |> List.map (fun input -> makeRequest (postReq "/IProtocol/echoRecord" (toJson input)))
            |> List.map (fun output -> ofJson<Record> output)
            |> function 
                | [{ Prop1 = "hello"; Prop2 = 10; Prop3 = Some 5 }
                   { Prop1 = "";      Prop2 = 1;  Prop3 = None   } ] -> pass()
                | otherwise -> failUnexpect otherwise  
    ]
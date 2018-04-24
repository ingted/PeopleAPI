module PeopleDb.App

open System
open System.Collections.Generic
open WebSharper
open WebSharper.Sitelets

/// The types used by this application.
module Model =

    /// Data about a person. Used both for storage and JSON parsing/writing.
    type PersonData =
        {
            firstName: string
            lastName: string
            [<DateTimeFormat "yyyy-MM-dd">]
            born: System.DateTime
            /// Since this is an option, this field is only present in JSON for Some value.
            [<DateTimeFormat "yyyy-MM-dd">]
            died: option<System.DateTime>
        }

    /// The type of REST API endpoints.
    /// This defines the set of requests accepted by our API.
    type ApiEndPoint =

        /// Accepts GET requests to /people/{id}
        | [<EndPoint "GET /people">]
            GetPerson of id: int

        /// Accepts POST requests to /people with PersonData as JSON body
        | [<EndPoint "POST /people"; Json "personData">]
            CreatePerson of personData: PersonData

        /// Accepts PUT requests to /people/{id} with PersonData as JSON body
        | [<EndPoint "PUT /people"; Json "personData">]
            EditPerson of id: int * personData: PersonData

        /// Accepts DELETE requests to /people/{id}
        | [<EndPoint "DELETE /people">]
            DeletePerson of id: int

    /// The type of all endpoints for the application.
    type EndPoint =
        
        /// Accepts requests to /
        | [<EndPoint "/">] Home

        /// Accepts requests to /api/...
        | [<EndPoint "/api">] Api of ApiEndPoint

    /// Error result value.
    type Error = { error : string }

    /// Alias representing the success or failure of an operation.
    /// The Ok case contains a success value to return as JSON.
    /// The Error case contains an HTTP status and a JSON error to return.
    type ApiResult<'T> = Result<'T, Http.Status * Error>

    /// Result value for CreatePerson.
    type Id = { id : int }

open Model

/// This module implements the back-end of the application.
/// It's a CRUD application maintaining a basic in-memory database of people.
module Backend =

    /// The people database.
    /// This is a dummy implementation, of course; a real-world application
    /// would go to an actual database.
    let private people = new Dictionary<int, PersonData>()

    /// The highest id used so far, incremented each time a person is POSTed.
    let private lastId = ref 0

    let personNotFound() : ApiResult<'T> =
        Error (Http.Status.NotFound, { error = "Person not found." })

    let GetPerson (id: int) : ApiResult<PersonData> =
        lock people <| fun () ->
            match people.TryGetValue(id) with
            | true, person -> Ok person
            | false, _ -> personNotFound()

    let CreatePerson (data: PersonData) : ApiResult<Id> =
        lock people <| fun () ->
            incr lastId
            people.[!lastId] <- data
            Ok { id = !lastId }

    let EditPerson (id: int) (data: PersonData) : ApiResult<Id> =
        lock people <| fun () ->
            match people.TryGetValue(id) with
            | true, _ ->
                people.[id] <- data
                Ok { id = id }
            | false, _ -> personNotFound()

    let DeletePerson (id: int) : ApiResult<Id> =
        lock people <| fun () ->
            match people.TryGetValue(id) with
            | true, _ ->
                people.Remove(id) |> ignore
                Ok { id = id }
            | false, _ -> personNotFound()

    // On application startup, pre-fill the database with a few people.
    do List.iter (CreatePerson >> ignore) [
        { firstName = "Alonzo"
          lastName = "Church"
          born = DateTime(1903, 6, 14)
          died = Some(DateTime(1995, 8, 11)) }
        { firstName = "Alan"
          lastName = "Turing"
          born = DateTime(1912, 6, 23)
          died = Some(DateTime(1954, 6, 7)) }
        { firstName = "Bertrand"
          lastName = "Russell"
          born = DateTime(1872, 5, 18)
          died = Some(DateTime(1970, 2, 2)) }
        { firstName = "Noam"
          lastName = "Chomsky"
          born = DateTime(1928, 12, 7)
          died = None }
    ]

/// The server side website, tying everything together.
module Site =
    open WebSharper.UI
    open WebSharper.UI.Html
    open WebSharper.UI.Server

    let JsonContent (result: ApiResult<'T>) =
        match result with
        | Ok value ->
            Content.Json value
        | Error (status, error) ->
            Content.Json error
            |> Content.SetStatus status
        |> Content.WithContentType "application/json"

    let ApiContent (ep: ApiEndPoint) =
        match ep with
        | GetPerson id ->
            JsonContent (Backend.GetPerson id)
        | CreatePerson personData ->
            JsonContent (Backend.CreatePerson personData)
        | EditPerson (id, personData) ->
            JsonContent (Backend.EditPerson id personData)
        | DeletePerson id ->
            JsonContent (Backend.DeletePerson id)

    let HomePage (ctx: Context<EndPoint>) =
        // Type-safely creates the URI: "/api/people/1"
        let person1Link = ctx.Link (Api (GetPerson 1))
        Content.Page(
            Body = [
                p [] [text "API is running."]
                p [] [
                    text "Try querying: "
                    a [attr.href person1Link] [text person1Link]
                ]
            ]
        )

    let Main =
        Application.MultiPage (fun ctx endpoint ->
            match endpoint with
            | Home -> HomePage ctx
            | Api api -> ApiContent api
        )

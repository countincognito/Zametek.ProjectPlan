# RESTful API Guide

|  |  |
| --- | --- |
| **Version** | 1.2 |
| **Date** | 9 October 2026 |
| **Supersedes** | RESTful API Guide v0.x (5 October 2021) |

## 0. About this guide

### 0.1 Purpose and scope

This guide sets the conventions for the design of the HTTP APIs that you build and use. These APIs are resource-oriented request/response APIs. They exchange JSON (and, where this guide says so, other media types) over HTTP. The guide covers URIs, methods, representations, errors, headers, security, versioning and documentation.

The guide does not cover GraphQL, gRPC, WebSocket or server-sent-event APIs, or messaging. These technologies need their own guidance. REST over HTTP is the default only where its model fits: resources, cacheable reads and request/response.

### 0.2 How to read it

- The words MUST, MUST NOT, SHOULD, SHOULD NOT and MAY have the meaning that RFC 2119 and RFC 8174 give them. They have this meaning only when they are in capitals.
- Every rule has an identifier (`URI-3`, `SEC-7`). Reviews, lint rules and deviations use the identifier to cite the rule. The guide never reuses an identifier. A withdrawn rule keeps its number.
- A short reason, in italics, follows a rule where the reason is not obvious. Where this version is different from v0.x, Appendix C says what changed and why.
- Examples use `https://api.example.org` and a stations domain. JSON in examples is valid JSON. Path templates have braces: `/stations/{stationId}`.

### 0.3 Principles

1. **Build for the task of the consumer**, not for the database or the class model. Avoid one-to-one mappings from tables or methods to endpoints. Do not reveal details of the implementation (names of technologies, internal identifiers, stack traces).
2. **Use HTTP as the standard defines it** (RFC 9110). Methods, status codes, headers and media types have the meaning that the standard gives. Do not tunnel one method through another. Do not invent status codes.
3. **Be consistent.** An API spells, shapes and fails the same thing in the same way everywhere, in the API and across all APIs.
4. **Make change cheap.** Change an API only by additions. Expect unknown fields. Version explicitly. Deprecate in the open.
5. **Be secure by default.** TLS, authentication, least privilege and bounded use of resources are part of the design. They are not a later review.
6. **Prefer standards** (RFCs, OpenAPI, W3C Trace Context) to local inventions.

### 0.4 The short version

1. Resources are nouns: plural collections, hyphenated lowercase paths, and no verbs for create, read, update and delete (§1).
2. Use each HTTP method and status code as the standard defines it. Never return `200` for an error (§2, §6).
3. Use JSON for input and output, `lowerCamelCase` properties, RFC 3339 times, and strings for enums and identifiers (§3).
4. Errors are RFC 9457 problem details with a `traceId`. Validation errors list every problem (§7).
5. Collections have pages with cursors. They use flat query parameters for sorting and filtering (§5).
6. Use TLS 1.2 or later everywhere. Put credentials only in the `Authorization` header. Use OAuth 2.0 with PKCE for users (§10).
7. Authorize every request at the level of the object, the property and the function (§10.3).
8. Put the major version in the path. Change an API only by additions. Deprecate with headers (§12).
9. The OpenAPI description is the source of truth. Lint it, diff it, and run its examples in CI (§13).
10. Trace every request, expose health endpoints, and limit what one caller can demand (§11, §14).

### 0.5 Deviations

Where you cannot reasonably follow a rule, break it on purpose. Record the deviation and its reason in the OpenAPI description of the API (DOC-2). Take the deviation through design review (GOV-1). A deviation applies to that API only. It does not change this guide. A deviation from a security rule (`SEC-*`) is possible only after a security review.

## 1. Resources and URIs

### 1.1 Naming

- **URI-1** Paths MUST be lowercase. A multi-word segment MUST use hyphens: `/train-stations`, not `/trainStations` or `/train_stations`. *Paths are case-sensitive (RFC 3986). Thus, one spelling prevents duplicate resources and spurious `404` responses.*
- **URI-2** Paths MUST NOT have a trailing slash or a file extension. The client chooses the representation with `Accept`, not with `.json`.
- **URI-3** Resource names MUST be nouns. A **collection** is a plural noun (`/stations`). A **document** in a collection has its identifier in the path (`/stations/waterloo`). A **singleton**, which exists once for each parent, is a singular noun (`/stations/waterloo/profile`).
- **URI-4** Identifiers MUST be stable, opaque to clients and URL-safe. Prefer random UUIDs (version 4, RFC 9562) or other non-sequential values. Version 7 UUIDs sort by time of creation, but they also show that time. Thus, use them only where that is acceptable. A natural key such as `waterloo` MAY be an identifier if it is unique and never changes. Clients still treat it as opaque.

  An API SHOULD NOT expose sequential database keys. *Sequential keys make enumeration easy. Authorization (SEC-10) must hold in each case.*
- **URI-5** Create, read, update and delete MUST use HTTP methods. They MUST NOT use verbs in the path (`/getStation`, `/stations/create`).
- **URI-6** A relation that can exist only inside another resource SHOULD have a nested path (`/users/{userId}/messages`). Nesting SHOULD go no deeper than collection/id/collection/id. Beyond that, expose the child at the top level and filter it (`/messages?userId=12`).
- **URI-7** The path MUST hold the identity of a resource. The query string only filters, sorts, pages, selects and expands (§5).
- **URI-8** *Withdrawn.* Too vague to review. §5 covers filtering, sorting and search.
- **URI-9** *Withdrawn.* Moved to §0.2 as a notation convention.

### 1.2 Actions (procedural concepts)

Choose the first option in this list that fits:

- **ACT-1** Express a state change as a change to the resource: `PATCH /stations/waterloo` with `{ "status": "closed" }`.
- **ACT-2** If the change is a relationship or a flag, make it a sub-resource: `PUT /gists/{gistId}/star` and `DELETE /gists/{gistId}/star`. Both methods are idempotent.
- **ACT-3** Use a verb phrase as the last segment, with POST, only when neither of the first two options fits (a command that acts across resources, or computes a result): `POST /stations/waterloo/recalculate-capacity`. The verb is an imperative in kebab-case. The operation MUST NOT be a GET.
- **ACT-4** An action across several resource types has its own mapping: `/search`. A query too complex for a URL MAY be a `POST /search` with a JSON body. The documentation then says that the operation is safe (it changes nothing). The HTTP `QUERY` method is for exactly this case, but it is still an Internet-Draft with limited support in tools. Do not use it until it is an RFC and your clients and gateways support it.

## 2. HTTP methods

| Method | Use | Safe | Idempotent | Request body |
| --- | --- | --- | --- | --- |
| GET | Get a representation | yes | yes | SHOULD NOT |
| HEAD | The same as GET, but with no response body | yes | yes | SHOULD NOT |
| POST | Create a resource in a collection, do an action, or submit a query | no | no (see REQ-5) | yes |
| PUT | Replace a resource at a URI that the client knows | no | yes | yes |
| PATCH | Change part of a resource | no | not by definition | yes |
| DELETE | Remove a resource from its parent | no | yes | SHOULD NOT |
| OPTIONS | Say which methods a resource allows | yes | yes | no |

- **MTH-1** Methods MUST have their HTTP meaning. A message MUST NOT use GET or POST to tunnel other methods (`?_method=delete`, `X-HTTP-Method-Override`). A message MUST NOT use them in any other way that hides or misrepresents the intent of the message.
- **MTH-2** GET and HEAD MUST be safe. They MUST NOT cause a change that the client can observe or must answer for (logging and metrics excepted).
- **MTH-3** A POST that creates a resource MUST answer `201 Created` with a `Location` header that holds the URI of the new resource. The response SHOULD also return the representation of the resource.
- **MTH-4** PUT replaces the whole resource and is idempotent. It MAY create a resource when the client chooses the identifier (`201` on creation, `200` or `204` on replacement). A partial update MUST NOT use PUT.
- **MTH-5** PATCH makes a partial update. The `Content-Type` of the request MUST give the patch format. The documentation MUST list the properties that a client can change with a patch. Servers SHOULD advertise the formats that they accept in `Accept-Patch`.

  Use JSON Merge Patch (`application/merge-patch+json`, RFC 7396) for plain documents. In this format, a `null` clears a property, and an array replaces the old array whole. Use JSON Patch (`application/json-patch+json`, RFC 6902) when you need arrays, precise operations or an explicit `null` value.
- **MTH-6** Updates (PUT, PATCH, and POST actions that change a resource) SHOULD return the updated representation with `200`, or `204` with no body. A client MAY choose with `Prefer: return=minimal` or `return=representation` (RFC 7240). *This saves a second round trip.*
- **MTH-7** DELETE removes the resource from its parent and answers `204` (or `200` with a body, or `202` if the removal is asynchronous). An API MUST choose one behavior for a repeated DELETE, apply it consistently and document it. The choice is `404` (the default: it tells the client that the resource is gone) or `204` (it lets a client retry after a timeout without ambiguity). Clients MUST treat `404` on a retried DELETE as success.
- **MTH-8** OPTIONS MUST answer with `Allow`. An API that browsers call MUST also answer CORS preflight requests (SEC-15). OPTIONS is not a discovery mechanism. The OpenAPI description is the discovery mechanism (DOC-1).
- **MTH-9** A server MUST answer a method that the resource does not support with `405` and an `Allow` header.

## 3. Representations

- **REP-1** JSON (RFC 8259, UTF-8) is the default representation: `application/json`. Every message that has a body MUST carry a `Content-Type`. A server MUST answer an unsupported request media type with `415`. The server offers other representations (`application/zip`, `text/csv`, `image/png`) through `Accept` where they have a clear use.
- **REP-2** A JSON representation MUST be an object. A response MUST NOT return a bare array or scalar at the top level, because a client can never extend it. Collections follow §5.
- **REP-3** An API returns a single resource as itself, not inside a `data`/`meta`/`links` envelope. The status, the headers and `Location` carry what a wrapper carries. *v0.x suggested a wrapper and also warned against envelopes. This version resolves the conflict in favor of no wrapper.*
- **REP-4** Property names MUST be `lowerCamelCase` ASCII (`openedOn`, `stationId`). The same concept MUST have the same name everywhere. Names MUST be words, not abbreviations or type prefixes (`earnedValueChart`, not `ev`, and `name`, not `strName`). The common short forms `id`, `url` and `q` are the exceptions. An acronym is a word (`stationUrl`, `userId`).
- **REP-5** Enum values MUST be strings in `lowerCamelCase` (`"inService"`). They MUST NOT be numbers. Enums are open. A client MUST handle a value that it does not know, and treat the value as "other". The documentation MUST say so.
- **REP-6** Instants MUST be RFC 3339 date-times with `Z` or an offset (`2026-10-04T09:30:00Z`). Calendar dates have the form `2026-10-04`. Durations are ISO 8601 (`PT15M`). A message MUST NOT use an epoch number. A message MUST NOT use a time without an offset for an event that is in the past.

  An event in the future at a place (for example a timetabled departure) has a local date-time without offset (`2026-10-04T09:30:00`). In a separate property, it has the IANA zone name of the place (`Europe/London`). The reason is that zone rules can change before the event, and an instant alone is then wrong. Whenever the time zone is important, send its IANA name in a separate property.
- **REP-7** Identifiers MUST be strings. An integer that can be larger than 2^53 − 1 MUST be a string. Money MUST NOT be a floating-point number. Use a decimal string and an ISO 4217 code (`{ "amount": "12.50", "currency": "GBP" }`).

  Sometimes a model gives an estimate in floating point, in a unit that the model defines and with no currency (for example a forecast cost). This figure is a measurement and not an amount of money. It MAY be a JSON number. The documentation says what it measures and in what unit. Countries use ISO 3166-1 alpha-2. Languages use BCP 47.
- **REP-8** Booleans have the names of questions (`isOpen`, `hasLift`). A request member that asks for a part of the result is not a boolean but an object (`"project": {}`). It is present when the client wants the part, and absent when the client does not want it. *A flag called `project` is not a question, and an object can later carry settings without a breaking change.*

  `null` means "known, and has no value". An absent property means "not requested, unknown or not applicable". The documentation says which meaning applies. A response returns a property that a client cleared through JSON Merge Patch (MTH-5) as `null` if the property is nullable. Otherwise, the response omits it. An empty collection is `[]`, never `null`.
- **REP-9** A server MUST NOT localize machine-readable values (identifiers, enums, codes). Human text MAY follow `Accept-Language`. The response then carries `Content-Language`.
- **REP-10** A server SHOULD NOT pretty-print responses. Compact JSON, compressed (HDR-9), is smaller and costs the consumer nothing. Tools can format it. *v0.x asked for pretty-printing by default. Its own reason (bandwidth) supports the opposite choice.*
- **REP-11** Hypermedia is optional. If a representation links to other resources, it SHOULD use absolute URIs, in properties named `...Url`. The server MUST NOT force a client to build a URI that the server already made (`Location`, links).
- **REP-12** *Tolerant reader.* A client MUST ignore response properties that it does not know. A server SHOULD reject unknown properties in a request (`422`), so that a misspelled option fails loudly and not silently. The cost is that a client that is newer than the server (for example during a rolling deployment) gets a failure and not an ignored property. Thus, clients MUST NOT send a property until the server that they call releases it.

## 4. Requests

- **REQ-1** Input that is too complex for a URL (it has types and hierarchy) MUST be a JSON body with `Content-Type: application/json`. The URL carries only identity, filters, paging and projection.
- **REQ-2** A client sends files as `multipart/form-data` (the file parts, and any structured options as a part that has the type `application/json`) or as the request body in the media type of the file. The documentation gives the maximum size, and the server enforces it (`413`).
- **REQ-3** A server MUST validate every request against its schema, and MUST enforce limits on size, depth, length and counts. It answers with `400`, `413`, `415` or `422`, as §6 describes. Client-side validation is a convenience, not a control.
- **REQ-4** *Optimistic concurrency.* A resource that clients can update at the same time SHOULD carry a strong `ETag`. PUT, PATCH and DELETE on it SHOULD require `If-Match`. The server answers `428` when the request has no `If-Match`, and `412` when it no longer matches. `If-Match` uses strong comparison. Thus, the `ETag` MUST stay strong on the wire. Configure compression, gateways and CDNs so that they do not downgrade it to a weak `ETag` (HDR-9).

  *This replaces "stores must support conditional PUT", which covered one method of three.*
- **REQ-5** *Idempotency.* An operation that a client can need to retry after a timeout, and cannot safely repeat (a POST that moves money, sends a message or starts a job), SHOULD accept an `Idempotency-Key` header. The same key with the same payload returns the first result. The same key with a different payload answers `422`. A key applies only to its caller, and it expires after a documented period. *The header is an IETF Internet-Draft, but many systems implement it.*
- **REQ-6** `Accept` selects the response representation. A server MUST honor it where a resource has more than one representation, and SHOULD answer `406` when none is acceptable. Errors are always `application/problem+json`, whatever `Accept` says.
- **REQ-7** Clients SHOULD send a W3C `traceparent` header. Servers MUST accept it, propagate it, and generate one when it is absent (OPS-2).
- **REQ-8** Servers MUST set limits on the size and the duration of a request (RL-5) and document the limits.

## 5. Collections

### 5.1 The collection representation

- **COL-1** A collection response is an object with the items of the page in an array named `items`, and the paging state beside it. The response omits `nextCursor` on the last page. A server MAY also send `Link` headers (RFC 8288, `rel="next"`) for generic clients.

  This is the representation of a collection resource. A bounded array that is a member of another representation (the `outputs` of a result, the `scenarios` of a project's listing) is a plain array under a descriptive name, and it has no pages. An array that can grow without bound is a collection resource instead.

  *v0.x preferred `Link` headers to a wrapper. In browsers they need `Access-Control-Expose-Headers`. Some proxies and SDKs drop them, and they leave no room to grow. An object with `items` has room to grow, and it is not an envelope around a resource.*

```json
{
  "items": [
    { "id": "waterloo", "name": "Waterloo", "status": "open" },
    { "id": "victoria", "name": "Victoria", "status": "open" }
  ],
  "nextCursor": "eyJpZCI6InZpY3RvcmlhIn0"
}
```

### 5.2 Paging, sorting, filtering and selection

- **COL-2** Every collection that can grow MUST have pages, with a documented default and maximum page size. A server MUST NOT return an unbounded list.
- **COL-3** Paging SHOULD use cursors: `?limit=50&cursor=<opaque>`. A cursor is opaque to the client and survives inserts and deletes, and the server validates it. A server MAY offer offset paging (`?limit=50&offset=100`) only for small collections that change slowly. Offset paging skips and repeats items when the collection changes, and it slows with depth.
- **COL-4** A total count is expensive on large collections. Offer it only on request (`include=totalCount`), or as an estimate, and say which.
- **COL-5** `sort` takes a comma-separated list of property names. A leading `-` means descending: `?sort=-openedOn,name`. The documentation MUST state the default order, and this order MUST be total (tie-broken by identifier), or paging is not stable.
- **COL-6** Filters are flat parameters that have the name of the property: `?status=open&lineId=7`. Ranges use `...After`/`...Before` or `...Min`/`...Max` (`openedAfter=2026-01-01`). Free-text search is `q`. The documentation says if a match is exact, prefix or contains. Servers SHOULD NOT use bracketed parameters (`filter[status]`, `page[offset]`, `fields[station]`). Brackets must have percent-encoding (RFC 3986), many frameworks and gateways do not bind them, and flat names are as easy to read.
- **COL-7** `fields=id,name` selects the properties in the response (the response always has the identifier).
- **COL-8** `include=lines,customer` adds related resources. It also adds optional parts of the representation. The response does not return these parts by default because they are costly or rarely wanted. It lets a client get what it needs in one request.
- **COL-9** A server MUST answer an unknown query parameter, field name or `include` value with `400`. *A misspelled filter must not quietly return everything.*
- **COL-10** *Withdrawn.* Normalizing the order of parameters for caching is the job of the cache or CDN, not of clients.
- **COL-11** An API MAY offer aliases for frequent, stable queries (`/users/me`, `/orders?status=open`). The documentation describes them, and they do not change meaning.

## 6. Responses and status codes

- **STS-1** The status code describes the outcome of the request as a whole. A response MUST NOT use `200` to carry an error in the body.
- **STS-2** Use the most specific status code that the standard offers. Do not invent a status code. Appendix B lists the codes that this guide uses.
- **STS-3** Use `200` for success with a body, and `201` for creation (MTH-3). Use `202` when the server accepts the work but the work is not complete (§9). Use `204` when the body is intentionally empty.
- **STS-4** Use redirection for resources that moved. `308` (permanent) and `307` (temporary) keep the method and the body. A response MUST NOT use `301` or `302`, because clients can change a POST to a GET. Two other codes have their own jobs: `303` sends a client to the result of an operation, and `304` answers a conditional GET.
- **STS-5** `400` means that the server cannot understand the request: the syntax or framing is malformed, or a parameter is not valid. `422` means that the server understood the request and the request is well-formed, but it breaks the rules of the schema or the domain. `409` means that the request conflicts with the current state (a duplicate, a stale version).
- **STS-6** `401` means that the credentials are missing or wrong, and the response MUST carry `WWW-Authenticate`. `403` means that the caller is known and is not permitted. Where the statement that a resource exists is itself a disclosure, answer `404` and not `403`.
- **STS-7** Use `404` when a URI maps to no resource, and `410` when the resource is gone for good. Use `412`, `413`, `415`, `428` and `429` as Appendix B says.
- **STS-8** `500` means that the API malfunctioned. `501` means that the API does not support what the client asked. `502` and `504` mean that an upstream service failed or timed out. `503` means that the API is overloaded or down for maintenance, or that a request overran its processing limit (RL-5), and it carries `Retry-After`. If there are several problems of one kind, use the most general code that fits (`400`, `500`).
- **STS-9** An operation is atomic where it can be. If part of one operation fails, the response reports the failure with the status that fits its cause. A response MAY include partial results as documented extension members. A response with a success-class status reports individual failures only for batches (BLK-2).

## 7. Errors

- **ERR-1** Every `4xx` and `5xx` response MUST be a problem details object (RFC 9457) with `Content-Type: application/problem+json`. This includes the responses that frameworks and authentication produce (`401`, `404`, `405`). Errors from a gateway or other infrastructure SHOULD also use this format. Where that is not possible, the documentation records the exception (§0.5). A client MUST NOT have to handle a second error format from the API implementation itself.

  *RFC 9457 obsoletes RFC 7807. It replaces the JSON:API-style `errors` object of v0.x with a standard that generic clients and frameworks already understand.*
- **ERR-2** The standard members have these uses:
  - `type` is a stable, documented URI that identifies the kind of problem. Use `about:blank` only when the status says all that is necessary.
  - `title` is a short summary. It is the same for every occurrence of the `type`.
  - `status` is the HTTP status as a number.
  - `detail` is an explanation of this occurrence, for people.
  - `instance` is a URI that identifies this occurrence (for example `urn:uuid:...`).
- **ERR-3** Every problem MUST carry `traceId`, the trace identifier of the request (HDR-8). With this value, a person can match a report to the logs.
- **ERR-4** A problem about the content of the request MUST list every problem found, and not only the first, in an `errors` array. Each entry has `detail` and a location. The location is `pointer`, `parameter` or `header`. `pointer` is a JSON Pointer (RFC 6901) into the request body in URI-fragment form (`#/address/postcode`, as in the example of RFC 9457). `parameter` is a query or path parameter.

  An entry MAY have `code`, a short stable identifier for the rule that the request breaks (`required`, `outOfRange`). Body validation gives `422`. A bad parameter or malformed syntax gives `400`.
- **ERR-5** If one request has several problems, the status is the most generally applicable one (STS-8). A request with both body problems (`422`) and parameter or syntax problems (`400`) gets `400`. The `errors` array still lists all of them.
- **ERR-6** Errors MUST NOT reveal details of the implementation. They MUST NOT show stack traces, SQL, class names, file paths or host names, or anything that tells an attacker if an account exists. The cause of a `500` goes to the log, under the `traceId`.
- **ERR-7** `type` and `code` are for programs, and the server never localizes them. `title` and `detail` are for people. They MAY follow `Accept-Language`, and clients MUST NOT parse them. A client that does not know a `type` treats the problem as its status class.
- **ERR-8** The OpenAPI description (DOC-2) documents every `type` that an API can return, with an example.
- **ERR-9** HDR-6 lists the headers that must accompany particular error statuses (`WWW-Authenticate`, `Allow`, `Retry-After`).

```http
HTTP/1.1 422 Unprocessable Content
Content-Type: application/problem+json
Content-Language: en
Request-Id: 4bf92f3577b34da6a3ce929d0e0e4736

{
  "type": "https://api.example.org/problems/validation-failed",
  "title": "The request is not valid",
  "status": 422,
  "detail": "2 properties are not valid.",
  "instance": "urn:uuid:6e0a2d94-3f7f-4b1c-9c3e-1f5b7d2a8c10",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "errors": [
    { "pointer": "#/name", "code": "required", "detail": "must not be empty" },
    { "pointer": "#/openedOn", "code": "format", "detail": "must be a date, such as 2026-10-04" }
  ]
}
```

## 8. Headers and caching

- **HDR-1** Every message with a body MUST have a `Content-Type`. JSON does not need a `charset` (it is UTF-8). `Content-Length`, `Transfer-Encoding` and `Date` belong to the server and the protocol stack, not to the logic of the API. *v0.x asked for `Content-Length`. With chunked transfer and the framing of HTTP/2 and HTTP/3, it is no longer a rule at the level of the API.*
- **HDR-2** `Location` MUST accompany `201`, `202` (the status resource) and redirects.
- **HDR-3** Cacheable representations SHOULD carry a strong `ETag` and MAY carry `Last-Modified`. A conditional GET with `If-None-Match` or `If-Modified-Since` answers `304` when nothing changed. This saves bandwidth.
- **HDR-4** `Cache-Control` (RFC 9111) is the way to state caching. A server SHOULD NOT send `Expires` and `Pragma`, because newer headers replace them. Use `public, max-age=<seconds>` for reference data that changes rarely, with `stale-while-revalidate` where stale data is tolerable. Use `private` for data that belongs to the caller. Use `no-store` for personal or sensitive data (`no-cache` means "revalidate before reuse", not "do not store").

  An API SHOULD encourage caching wherever a cache can safely store the response to a GET. Freshness headers belong on `200`, and MAY be on `308`, `404` and `410`.
- **HDR-5** `Vary` MUST list every request header that changes the representation (`Accept`, `Accept-Language`, `Accept-Encoding`).
- **HDR-6** `Allow` MUST accompany `405`. `WWW-Authenticate` MUST accompany `401`. `Retry-After` MUST accompany `429` and `503`. `Accept-Patch` SHOULD accompany resources that support PATCH.
- **HDR-7** A custom header MUST NOT change the behavior of an HTTP method or replace a standard header. The name of a new custom header MUST NOT begin with `X-` (RFC 6648), and SHOULD be descriptive (`Request-Id`). The OpenAPI description MUST document each custom header. Prefer the standard header where there is one (`Authorization`, `traceparent`, `Retry-After`, `Idempotency-Key`).
- **HDR-8** Every response SHOULD carry a `Request-Id`: the trace identifier of the request (32 hexadecimal characters). It is the same value as `traceId` in error bodies and in the logs of the server.
- **HDR-9** Servers SHOULD compress responses for clients that accept compression (`gzip`, `br`, and `zstd` where supported) and MUST set `Vary: Accept-Encoding`. A server does not compress media that is already compressed (images, archives) a second time.
- **HDR-10** A deprecated operation carries `Deprecation` (RFC 9745). When the removal has a date, it also carries `Sunset` (RFC 8594). `Link` relations `deprecation` and `sunset` point to the migration guide (VER-5).
- **HDR-11** SEC-15 and SEC-16 cover the security headers.

## 9. Long-running and bulk operations

- **ASY-1** An operation that you cannot rely on to finish within a few seconds SHOULD be asynchronous. It answers `202 Accepted` with a `Location` header for a status resource, and `Retry-After` as a hint. An operation MAY stay synchronous when it has a short, documented upper bound. Past that bound it is `202`.

  A service that keeps nothing between requests (SEC-9) has nowhere to keep a job. It MAY instead stop the operation at the bound and answer as RL-5 says. It records that choice in its OpenAPI description (§0.5).
- **ASY-2** The status resource (`GET /operations/{operationId}`) reports a state (`running`, `succeeded`, `failed`, `cancelled`) and MAY report progress. On success, it links to the result (or redirects to it with `303`). On failure, it embeds a problem (§7). Clients MUST NOT poll faster than `Retry-After`.
- **ASY-3** A client cancels a running operation with `POST /operations/{operationId}/cancel` (ACT-3). After that, the status resource reports `cancelled`. The server keeps a status resource for a documented time. A server MAY also offer signed webhooks. A webhook is a notification, and never the only way to learn the outcome.
- **BLK-1** Use one resource for each request where you can. Offer a batch operation only where round trips are the problem, and document its maximum size.
- **BLK-2** A batch SHOULD be atomic. Where it cannot be, answer `207 Multi-Status` with an `items` array. The array has one entry for each input, in order. Each entry has its own `status` and either the result or a problem. This is the one place where a success-class status reports individual failures.

## 10. Security

### 10.1 Transport

- **SEC-1** Every endpoint that clients can reach from beyond the computer that it runs on MUST use TLS. ("TLS everywhere": SSL is obsolete.) Such an endpoint MUST NOT also serve plain HTTP, not even to redirect. A client that sends a credential over HTTP exposes it. An endpoint that only local programs reach (loopback, a Unix domain socket) MAY use plain HTTP.

  TLS MAY terminate at a load balancer, ingress or service-mesh proxy, with plain HTTP only on the last hop inside a trusted, access-controlled network (including probes from the orchestrator). The documentation describes that arrangement. Certificate issuance MUST NOT need a plain-HTTP listener on the API host (use DNS-01 or a dedicated validation endpoint).
- **SEC-2** TLS 1.2 is the minimum, and servers SHOULD prefer TLS 1.3. Servers MUST disable TLS 1.0 and 1.1 (RFC 8996). Cipher suites follow RFC 9325: only authenticated encryption with forward secrecy (ECDHE), which TLS 1.3 gives by default. Certificates come from a trusted authority and renew automatically. CAA records and Certificate Transparency monitoring watch them.
- **SEC-3** A host that browsers can reach SHOULD send `Strict-Transport-Security` (RFC 6797) with a `max-age` of at least one year and `includeSubDomains`. It MUST NOT send this header on `localhost` or development hosts, where a browser remembers it.
- **SEC-4** Servers MUST NOT use HTTP Public Key Pinning (browsers withdrew it). A native client MAY pin only with a reviewed plan for rotation and recovery.
- **SEC-5** Service-to-service calls SHOULD use mutual TLS or sender-constrained tokens (SEC-7) where the data needs this protection.

### 10.2 Authentication

- **SEC-6** Credentials, tokens and API keys MUST NOT appear in a URL, in the path or the query. Systems log URLs, keep them in histories and send them in `Referer`. Credentials go in the standard `Authorization` header (not a custom header). A body carries a secret only at a dedicated token endpoint.

  Sensitive personal data (government identifiers, health or financial data, special-category data) SHOULD NOT appear in a URL either. Look such a resource up with a body, as in ACT-4. Ordinary identifiers and filters (`/users/{userId}`, `?lineId=7`) are acceptable.
- **SEC-7** For access on behalf of a user, use OAuth 2.0 with OpenID Connect: the authorization-code flow with PKCE (RFC 7636). For service-to-service access, use the client-credentials flow. A client MUST NOT use the implicit flow or the resource-owner password flow. RFC 9700 forbids the second flow and advises against the first, and the OAuth 2.1 draft removes both.

  Access tokens SHOULD be short-lived (an hour or less), and refresh tokens SHOULD rotate. A server MUST validate a JWT access token fully (RFC 9068). It checks the signature against an allow-list of algorithms (never `none`), and it checks `iss`, `aud`, `exp` and `nbf`. It fetches the keys from the JWKS of the issuer and rotates them. High-risk APIs SHOULD bind tokens to the sender (DPoP, RFC 9449, or mutual TLS, RFC 8705).
- **SEC-8** A static API key MAY protect low-risk, server-to-server access. The key MUST meet these conditions:
  - It has at least 128 bits of entropy.
  - The server stores it only as a hash.
  - The server compares it in constant time.
  - It has a scope, and you can revoke it and rotate it.
  - It goes in the header `Authorization: Bearer <key>`.
- **SEC-9** An API MUST be stateless. This means that a server needs no server-side session to process a request. A server MAY use cookies for browser clients only. They are `Secure; HttpOnly; SameSite` cookies, with protection against cross-site request forgery.

### 10.3 Authorization and data

- **SEC-10** The server MUST authorize every request, except requests that the documentation declares public. Public requests are the CORS preflight (browsers send it without credentials), the health endpoints of OPS-1, and the OpenAPI description and a JWKS, if the server supplies them. Authorization has three levels:
  - The **object**: is this caller permitted to touch this identifier? (OWASP API1)
  - The **property**: is the caller permitted to read or write this field? (API3) Writable properties are an allow-list, which prevents mass assignment.
  - The **function**: is the caller permitted to call this operation? (API5)

  Roles and scopes are least privilege.
- **SEC-11** The server MUST NOT trust identity, roles, prices or other values that the client supplies when the server can derive them. The server derives them from the token and its own data.
- **SEC-12** Responses MUST return only what the caller needs. The owner MUST classify sensitive data, and MUST handle personal data under the privacy policy. Logs MUST NOT contain sensitive data or personal data.
- **SEC-13** STS-6 sets the status for unauthenticated and unauthorized callers. A failed sign-in MUST NOT say which part was wrong.

### 10.4 Input and resources

- **SEC-14** The server validates input against a schema (types, ranges, formats, enums), rejects unknown properties, and limits size, depth, array length and string length. It reaches data stores only through parameterized queries or safe APIs. Uploaded files get a check for type and size, and the server stores them away from anything executable.

  A server MUST NOT fetch a URL that a caller supplies without defenses against server-side request forgery (API7). Use an allow-list where the destinations are known. Otherwise, use these defenses: HTTPS only, resolution checks that block loopback, link-local and private ranges, no followed redirects, and egress filtering. This rule covers webhook destinations (ASY-3).
- **SEC-15** An API enables CORS only if browsers call it. The configuration has an explicit allow-list of origins, the fewest methods and headers that are necessary, and a lifetime for the preflight cache. A server MUST NOT combine `Access-Control-Allow-Origin: *` with credentials.
- **SEC-16** Every response carries the correct `Content-Type` and `X-Content-Type-Options: nosniff`. A response that contains personal or sensitive data carries `Cache-Control: no-store`. An API whose responses a browser can frame or render MAY add `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`.

### 10.5 Operating securely

- **SEC-17** The system MUST log authentication events, authorization failures and validation spikes with the trace identifier, and SHOULD raise an alert on them. Logs MUST NOT contain secrets, tokens or personal data. Privileged actions MUST be auditable.
- **SEC-18** Every API, version and endpoint MUST be in an inventory. Teams MUST retire a version when it is past its sunset date (API9). There MUST be no undocumented endpoints. Teams MUST scan and patch dependencies. Secrets MUST live in a secrets manager, and MUST NOT be in source or in images.
- **SEC-19** Responses from other APIs that the service calls are untrusted input. The service MUST validate them, MUST call those APIs with timeouts, and MUST fetch the responses over TLS (API10).
- **SEC-20** The OWASP API Security Top 10 (2023) is the minimum threat checklist. The team MUST review each release against it (Appendix A).

## 11. Rate limiting and resource protection

- **RL-1** Every API MUST set limits on the work that one caller can demand (rates, quotas, concurrency) and MUST document the limits (OWASP API4).
- **RL-2** A caller that is over a limit gets `429 Too Many Requests`. A server that is overloaded, or that drains its connections, answers `503` to any caller. Both responses carry `Retry-After` (seconds or an HTTP date).
- **RL-3** Responses SHOULD carry the state of the rate limit in return headers, so that callers can see the rules. These headers are the IETF `RateLimit-Policy` and `RateLimit` fields. These fields are still an Internet-Draft (draft-ietf-httpapi-ratelimit-headers), and they changed between drafts. Until they are an RFC, document exactly which fields the API sends. The older `X-RateLimit-Limit`, `X-RateLimit-Remaining` and `X-RateLimit-Reset` are common, but they are not a standard.
- **RL-4** Clients MUST honor `Retry-After`, and back off exponentially with jitter on `429`, `503` and `504`. They retry only requests that are idempotent or that carry an `Idempotency-Key`. They do not retry a problem whose documented `type` says that a retry will not help (RL-5).
- **RL-5** The documentation MUST state the limits on the size and the duration of a request. The server MUST enforce them with `413`, `414`, `431` and `408` (the client was too slow to send the request). A request that exceeds the processing limit of the server gets `503` with `Retry-After`, or becomes asynchronous (ASY-1). `504` is for an upstream service that timed out.

  Clients retry a `503` (RL-4). Thus, a server SHOULD answer `422` instead, with a problem `type` that says so. This applies when the server can tell that the request is too large for the limit, however idle the server is. Where the server cannot tell load from size, it answers `503`. Then the problem `type` says that the request exceeded the limit and that a retry can take as long again.

  *A retry of work that is too large costs the server the same limit again.*

## 12. Versioning and evolution

- **VER-1** Every API MUST have a version from its first release. The major version is the first path segment (`/v1/stations`), which is visible in logs, caches and `curl`. The query string MUST NOT carry the major version, and a media type SHOULD NOT carry it. A large public API MAY add a date version that a header selects (as Stripe does). It is not the default.
- **VER-2** Within a major version, changes MUST be additive. These changes are compatible:
  - New endpoints.
  - New optional request properties and parameters.
  - New response properties.
  - New enum values (REP-5).
  - New optional headers.
  - New problem `type`s.
  - Looser validation.
- **VER-3** Breaking changes need a new major version. These changes break clients:
  - The removal or the renaming of anything.
  - A change to a type, unit, format or meaning.
  - A change that makes an optional item required.
  - Tighter validation.
  - A change to a status code or problem `type` for an existing case.
  - A change to a default order or page size.
  - The removal of an enum value.
  - A change to authentication.
- **VER-4** Consumers MUST tolerate the additive changes of VER-2 (REP-12).
- **VER-5** The owner of an API deprecates a version or an operation in the open. The owner announces it with a migration guide and marks it with `Deprecation` and `Sunset` headers (HDR-10). The owner serves it together with its replacement for the notice period, and watches it through usage metrics for each version. The owner removes it on the stated date. The audience sets the minimum notice: three months for internal consumers, and twelve months for external consumers.
- **VER-6** The OpenAPI description carries the full semantic version (`info.version`). The path carries only the major version.

## 13. Documentation, testing and governance

- **DOC-1** Every API MUST have an OpenAPI description (version 3.1 or later, and 3.2 is current) as its single source of truth. The description lives in source control beside the source code and changes with it. The owner publishes it where consumers find it: the API supplies it, or a developer portal publishes it. ("Swagger" is the old name of the specification. Swagger UI and Swagger Editor are tools that read it.)
- **DOC-2** The description MUST cover all of these items:
  - Every operation, parameter and schema.
  - Every status code that the API returns.
  - Every problem `type`, with an example.
  - The security schemes.
  - The rate limits.
  - Any deviation from this guide (§0.5).

  Each operation MUST have an example of a full request/response cycle, and SHOULD have an example for an error that is specific to it. Do not repeat the common errors. The guide shows them one time.
- **DOC-3** CI MUST lint the description against this guide (for example with a Spectral ruleset). For an API with consumers outside the owning team, CI MUST also compare the description with the last release for breaking changes (for example with oasdiff). For other APIs, this comparison is a SHOULD. Contract tests generated from the description SHOULD run against the implementation, so that the document and the source code cannot drift apart.
- **DOC-4** Reference documentation SHOULD come from the description (Redoc, Scalar, Swagger UI), with written guides for getting started, authentication, errors, paging and the changelog. CI SHOULD execute the examples in the documentation. Teams SHOULD NOT write them by hand.
- **DOC-5** Every release that changes the API MUST have a changelog entry, and a migration guide if the release deprecates something.
- **GOV-1** A new API, and any breaking change, needs design review against Appendix A before the team builds it. The team records deviations (§0.5).
- **GOV-2** This guide has versions, and it records its changes (Appendix C).

## 14. Operations

- **OPS-1** A service SHOULD expose `GET /health/live` (the process is up) and `GET /health/ready` (the service can take traffic, and answers `503` until it can). These endpoints have no version and need no credentials. They carry `Cache-Control: no-store` and return no internal data. Detailed diagnostics MUST require authentication.
- **OPS-2** Servers MUST accept, propagate and generate W3C Trace Context (`traceparent`, `tracestate`). Servers MUST write structured logs that carry the trace identifier.
- **OPS-3** Servers MUST record metrics (rate, errors, duration) for each route template (`/stations/{stationId}`) and not for each URL, and for each API version. Each API MUST state its objectives for availability and latency.
- **OPS-4** Servers SHOULD stop gracefully. They refuse new work, let requests in progress finish within a documented time, and then exit.

## Appendix A. Design review checklist

| Check | Rules |
| --- | --- |
| Resources are nouns. Collections are plural. Paths are lowercase and hyphenated, with no verbs or extensions. | URI-1 to URI-7 |
| An action is a state change or a sub-resource before it is a verb. Complex queries are safe POSTs. | ACT-1 to ACT-4 |
| Each method means what HTTP says. Creation is `201` with `Location`. | MTH-1 to MTH-9 |
| Representations are objects in `lowerCamelCase`. Enums are strings. Times follow RFC 3339. | REP-1 to REP-12 |
| Every unsafe operation has a decision for concurrency and idempotency. | REQ-4, REQ-5 |
| Every collection has pages, a deterministic sort, and filters with flat parameters. | COL-1 to COL-11 |
| Status codes are correct, and no error hides in a `200`. | STS-1 to STS-9 |
| Every error is problem details with a `traceId`. Validation errors list every problem. | ERR-1 to ERR-9 |
| Each resource has a decision for caching. `ETag` is present where it helps. | HDR-3 to HDR-5 |
| A long operation is `202` with a status resource, or it has a documented bound. Batches have a bound. | ASY-1 to ASY-3, BLK-1, BLK-2 |
| TLS is 1.2 or later. No credentials are in URLs. The standard `Authorization` header carries credentials. | SEC-1 to SEC-8 |
| Authorization has a check for each object, property and function, and a test with the identifiers of another caller. | SEC-10, SEC-11 |
| The API validates and limits input, and rejects unknown properties. | REQ-3, SEC-14 |
| The API has a review against the OWASP API Security Top 10. | SEC-20 |
| The API documents its limits. It answers `429` or `503` with `Retry-After`. | RL-1 to RL-5 |
| The version is in the path. No breaking change occurs inside a major version. | VER-1 to VER-4 |
| The OpenAPI description is complete, linted and diffed, and has examples. | DOC-1 to DOC-4 |
| Health endpoints, tracing, logging and graceful shutdown are in place. | OPS-1 to OPS-4, SEC-17 |
| The team records deviations. | §0.5 |

## Appendix B. Status codes

| Code | Name | Use |
| --- | --- | --- |
| 200 | OK | Success with a body. Never carries an error. |
| 201 | Created | The server created a resource. The response has `Location`, and the body SHOULD be the representation of the resource. |
| 202 | Accepted | The server accepted the work, and the work is not finished. `Location` is the URI of the status resource. |
| 204 | No Content | Success. The body is intentionally empty. |
| 207 | Multi-Status | A batch with an outcome for each item (BLK-2). |
| 303 | See Other | Go to another URI and do a GET there to get the result of an operation. |
| 304 | Not Modified | A conditional GET found that nothing changed. |
| 307 | Temporary Redirect | The resource is temporarily at another URI. The method stays the same. |
| 308 | Permanent Redirect | The resource is permanently at another URI. The method stays the same. |
| 400 | Bad Request | The server cannot understand the request: it is malformed, or a parameter is not valid. |
| 401 | Unauthorized | Credentials are missing or not valid. The response has `WWW-Authenticate`. |
| 403 | Forbidden | The caller is known and is not permitted. |
| 404 | Not Found | There is no resource at the URI, or the server must not disclose that the resource exists. |
| 405 | Method Not Allowed | The resource does not support the method. The response has `Allow`. |
| 406 | Not Acceptable | None of the representations in `Accept` is available. |
| 408 | Request Timeout | The client took too long to send the request. |
| 409 | Conflict | The request conflicts with the current state. |
| 410 | Gone | The server removed the resource for good. |
| 412 | Precondition Failed | `If-Match` (or another precondition) no longer holds. |
| 413 | Content Too Large | The request body is over the limit. |
| 414 | URI Too Long | The URI is over the limit. |
| 415 | Unsupported Media Type | The server does not support the `Content-Type` of the request (or the patch format). |
| 422 | Unprocessable Content | The request is well-formed, but it breaks the rules of the schema or the domain, or it is too large for a limit (RL-5). |
| 428 | Precondition Required | `If-Match` is required, and the request does not have it. |
| 429 | Too Many Requests | The caller is over a limit. The response has `Retry-After`. |
| 431 | Request Header Fields Too Large | The headers are over the limit. |
| 500 | Internal Server Error | The API malfunctioned. |
| 501 | Not Implemented | The API does not support what the client asked. |
| 502 | Bad Gateway | An upstream service returned an invalid response. |
| 503 | Service Unavailable | The API is overloaded or in maintenance, or a request overran the processing limit of the server (RL-5). The response has `Retry-After`. |
| 504 | Gateway Timeout | An upstream service timed out. |

A response MUST NOT use `301` or `302` (STS-4).

## Appendix C. Changes from v0.x

The column Kind has these values: **C** is a correction (a fact or a defect), **M** is a modernization (a choice), and **N** is new.

| # | v0.x | v1.2 | Why | Kind |
| --- | --- | --- | --- | --- |
| 1 | No definition of "must", "should" and "may". No rule identifiers. | RFC 2119 and RFC 8174 keywords, rule identifiers, reasons, and a process for deviations | People can cite, lint and review the rules | N |
| 2 | "A singular noun for object instances" (`/stations/waterloo`) | Correct names for collection, document and singleton. Identifiers are stable, opaque and non-sequential. | `waterloo` is an identifier, and not a noun that someone chooses | M |
| 3 | "Lowercase should be preferred" | Lowercase MUST. Hyphenated segments. No extensions and no trailing slash. | One spelling for each resource | M |
| 4 | Nested relations inline | A limit on the depth of nesting | Deep paths tie clients to the hierarchy | M |
| 5 | Verbs for procedural concepts | State change first, then sub-resource, then verb | Fewer RPC-style endpoints | M |
| 6 | PUT "inserts and updates", and again "updates mutable resources" | PUT replaces. PATCH (JSON Merge Patch or JSON Patch) is the partial update. | PATCH appeared in an example, but v0.x never defined it | C |
| 7 | OPTIONS "should be used to retrieve metadata" | OPTIONS answers `Allow` and CORS preflight. OpenAPI is the discovery mechanism. | In practice, OPTIONS is the preflight request | M |
| 8 | `:id` in paths | `{id}` | The notation of OpenAPI | C |
| 9 | JSON input | Kept. `multipart/form-data` for files. | v0.x did not cover uploads | N |
| 10 | Pagination by `Link` header. `Total-Count` header. | `items` array and `nextCursor` in the body, cursor paging by default, total only on request | Headers are awkward for browsers, proxies and SDKs. Offsets drift. | M |
| 11 | `page[offset]`, `filter[field]`, `fields[resource]` | Flat, `lowerCamelCase` parameters | Brackets need percent-encoding, and frameworks bind them badly | M |
| 12 | `include`, `fields`, `sort` | Kept. Unknown parameters are `400`. | A typo must not return everything | M |
| 13 | "Include rate limiting information in return headers" | `429` and `503` with `Retry-After`. The guide marks the `RateLimit` fields as a draft. | v0.x did not give the status code to use | M |
| 14 | An optional `data`/`errors`/`meta`/`links` wrapper, and also "avoid envelopes" | No wrapper. Collections are an object with `items`. | The two rules contradicted each other | M |
| 15 | `errors` objects with string `status`, `code`, `links`, `paths` | RFC 9457 problem details. `errors[]` entries with `pointer`, `code` and `detail`. Numeric `status`. | A standard and not a house format. One pointer and not two. | M |
| 16 | JSON examples with typographic quotes and a missing bracket | Valid JSON | The examples did not parse | C |
| 17 | A PATCH body in the JSON:API shape | Merge Patch and JSON Patch | Standard patch formats | M |
| 18 | `Content-Length` "should be used" | Not a rule at the level of the API | Chunked transfer, HTTP/2 and HTTP/3 | C |
| 19 | `Last-Modified` and `ETag` "should be used" | Strong `ETag` SHOULD. `Last-Modified` MAY. `304`. | `ETag` is exact. `Last-Modified` has a resolution of one second. | M |
| 20 | "Stores must support conditional PUT" | `If-Match` on PUT, PATCH and DELETE. `412` and `428`. | All three methods can lose updates | M |
| 21 | `Cache-Control`, `Expires` and `Pragma` | `Cache-Control` and `Vary` | Newer headers replace `Expires` and `Pragma` | C |
| 22 | Custom headers must not change the behavior of a method | Kept. No `X-` prefix. Documented. | RFC 6648 | M |
| 23 | (absent) | `Request-Id`, `traceparent`, `Deprecation`, `Sunset`, compression, `Idempotency-Key` | Operability and lifecycle | N |
| 24 | `301` "to relocate". `302` "should not be used". | `308` and `307`. `303`. A response MUST NOT use `301` or `302`. | `301` and `302` let clients change the method | C |
| 25 | `408` without a description. No `409`, `410`, `413`, `415`, `428`, `429`, `502`, `503` and `504`. | Appendix B | The rules already used these status codes | C |
| 26 | `400` "may be used to indicate nonspecific failure" | `400` malformed. `422` not valid. `409` conflict. | A usable split | M |
| 27 | "SSL everywhere" | "TLS everywhere". No plain-HTTP twin for a remote endpoint. | SSL is obsolete | C |
| 28 | "TLS 1.1 (or greater)" | TLS 1.2 minimum, 1.3 preferred. 1.0 and 1.1 disabled. | RFC 8996 deprecated them in 2021 | C |
| 29 | "Public key pinning where possible" | Removed | Browsers withdrew HPKP | C |
| 30 | "Perfect Forward Secrecy where possible" | Required: ECDHE or TLS 1.3 | It is the default now | C |
| 31 | "HTST where possible" | HSTS for hosts that browsers reach. Never on `localhost`. | The name was a typo. The scope is now limited. | C |
| 32 | "Authentication in a custom header" | The standard `Authorization` header. Nothing secret in URLs. | Every client and log filter understands standard headers | C |
| 33 | "No cookies or sessions" | Stateless. Cookies only for browser clients, with flags and CSRF protection. | Browser-based clients exist | M |
| 34 | "OAuth 2.0 when a token isn't secure enough" | Which flows to use, token lifetimes, JWT validation, and sender-constrained tokens. The guide bans the implicit and password flows. | RFC 9700, RFC 9068 | C |
| 35 | (absent) | Authorization at three levels, input validation, SSRF, CORS, security headers, logging, inventory, and the OWASP API Security Top 10 | The usual causes of breaches | N |
| 36 | "Swagger documentation is preferable" | OpenAPI 3.1 or later as the source of truth. Lint, diff and contract tests. CI runs the examples. | Documentation that cannot drift | M |
| 37 | Versioning links. A date-header option. | Major version in the URL. A policy of additive changes only. A definition of breaking changes. Deprecation headers. Notice periods. No stale links. | A policy, not a reading list | M |
| 38 | "Pretty print by default and support gzip" | Compact JSON, compressed | Pretty-printing costs bytes | C |
| 39 | "Avoid envelopes unless the client can't use headers (JSONP)" | JSONP is obsolete (use CORS). See rows 10 and 14. | JSONP is gone | C |
| 40 | (absent) | Long-running operations, batches, health, tracing, a checklist and a status-code appendix | Gaps | N |

## Appendix D. Worked example

Create a station. The key makes the request safe to retry:

```http
POST /v1/stations HTTP/1.1
Host: api.example.org
Authorization: Bearer <token>
Content-Type: application/json
Idempotency-Key: 7b1f3e0a-6d0e-4a52-9a53-1c0b6a0a4d11

{ "name": "Waterloo", "openedOn": "1848-07-11" }
```

```http
HTTP/1.1 201 Created
Location: https://api.example.org/v1/stations/waterloo
Content-Type: application/json
ETag: "1"
Request-Id: 4bf92f3577b34da6a3ce929d0e0e4736
Cache-Control: no-store

{ "id": "waterloo", "name": "Waterloo", "openedOn": "1848-07-11", "status": "open" }
```

Change one property if nobody else changed the station:

```http
PATCH /v1/stations/waterloo HTTP/1.1
Host: api.example.org
Authorization: Bearer <token>
Content-Type: application/merge-patch+json
If-Match: "1"

{ "status": "closed" }
```

```http
HTTP/1.1 200 OK
Content-Type: application/json
ETag: "2"

{ "id": "waterloo", "name": "Waterloo", "openedOn": "1848-07-11", "status": "closed" }
```

The same request with a stale `If-Match`:

```http
HTTP/1.1 412 Precondition Failed
Content-Type: application/problem+json

{
  "type": "https://api.example.org/problems/stale-version",
  "title": "The station has changed",
  "status": 412,
  "detail": "Fetch the station again and reapply the change.",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

Get a page of open stations, two at a time, in order of name:

```http
GET /v1/stations?status=open&sort=name&limit=2 HTTP/1.1
Host: api.example.org
Authorization: Bearer <token>
Accept: application/json
```

```http
HTTP/1.1 200 OK
Content-Type: application/json
Cache-Control: private, max-age=30
ETag: "9f2c"
Vary: Accept, Accept-Encoding

{
  "items": [
    { "id": "euston", "name": "Euston", "status": "open" },
    { "id": "victoria", "name": "Victoria", "status": "open" }
  ],
  "nextCursor": "eyJuYW1lIjoiVmljdG9yaWEiLCJpZCI6InZpY3RvcmlhIn0"
}
```

## Appendix E. References

**Standards**

- RFC 2119 and RFC 8174: key words for requirement levels
- RFC 3339: date and time on the internet (timestamps)
- RFC 3986: URI generic syntax
- RFC 5789: the PATCH method
- RFC 6585: additional status codes (`428`, `429`, `431`)
- RFC 6648: deprecating the `X-` prefix
- RFC 6750: OAuth 2.0 bearer token usage
- RFC 6797: HTTP Strict Transport Security
- RFC 6901: JSON Pointer
- RFC 6902: JSON Patch
- RFC 7240: the `Prefer` header
- RFC 7396: JSON Merge Patch
- RFC 7636: PKCE
- RFC 8259: JSON
- RFC 8288: web linking
- RFC 8446: TLS 1.3
- RFC 8594: the `Sunset` header
- RFC 8705: OAuth 2.0 mutual-TLS client authentication and certificate-bound tokens
- RFC 8996: deprecating TLS 1.0 and 1.1
- RFC 9068: JWT profile for OAuth 2.0 access tokens
- RFC 9110: HTTP semantics
- RFC 9111: HTTP caching
- RFC 9325: recommendations for secure use of TLS and DTLS
- RFC 9449: DPoP
- RFC 9457: problem details for HTTP APIs (obsoletes RFC 7807)
- RFC 9562: UUIDs (including version 7)
- RFC 9700: best current practice for OAuth 2.0 security
- RFC 9745: the `Deprecation` header
- OpenAPI Specification 3.1 and 3.2 (the release of 3.2.0 was in September 2025)
- JSON Schema 2020-12
- W3C Trace Context
- OWASP API Security Top 10 (2023) and the OWASP REST Security Cheat Sheet

**Internet-Drafts** (status in October 2026. Check the IETF datatracker before you rely on them)

- `draft-ietf-httpapi-ratelimit-headers`: the `RateLimit` and `RateLimit-Policy` fields
- `draft-ietf-httpapi-idempotency-key-header`: the `Idempotency-Key` field
- `draft-ietf-httpbis-safe-method-w-body`: the `QUERY` method

**Other guidance used**

- Microsoft REST API Guidelines, Google API Improvement Proposals and Zalando RESTful API Guidelines
- jsonapi.org: the basis of the wrapper and the bracketed parameters of v0.x. This version does not adopt them.
- M. Masse, *REST API Design Rulebook* (O'Reilly, 2011): the origin of the URI naming rules. It is older than most of the standards above.

## Appendix F. Revision history

| Version | Date | Changes |
| --- | --- | --- |
| 1.0 (draft) | 4 October 2026 | The first revision since v0.x. Appendix C lists every change. |
| 1.1 | 4 October 2026 | The owner reviewed the draft. The MUSTs are stricter for traceability, security and operations (ERR-3, HDR-6, SEC-12, SEC-17 to SEC-20, OPS-2, OPS-3). TLS can terminate at a proxy (SEC-1). A request over the limit of the server is `503`, and `504` is for an upstream service (RL-5). The duties for documentation are clearer (DOC-2 to DOC-5). URI-8, URI-9 and COL-10 are withdrawn. |
| 1.1 (clarified) | 4 October 2026 | The application of the guide to an API clarified five points. A request too large for a limit is `422`, and load is `503` (RL-4, RL-5, STS-8, Appendix B). An estimate does not have to be a decimal string (REP-7). A request member that asks for a part of the result is an object (REP-8). A bounded array inside a representation is not a collection (COL-1). A service that keeps nothing can stop at its bound, and does not have to answer `202` (ASY-1). |
| 1.2 | 9 October 2026 | This version corrects SEC-12. Logs MUST NOT contain sensitive data or personal data. The previous text said that neither kind of data MAY appear in logs. This did not forbid it clearly. The text of the guide now loosely follows the writing rules of ASD-STE100 Issue 9. The obligations of the other rules are the same. |

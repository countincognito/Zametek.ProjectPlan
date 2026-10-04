# RESTful API Guide

|  |  |
| --- | --- |
| **Version** | 1.1 |
| **Date** | 4 October 2026 |
| **Supersedes** | RESTful API Guide v0.x (5 October 2021) |

## 0. About this guide

### 0.1 Purpose and scope

This guide sets the conventions for HTTP APIs that we design, build and consume: resource-oriented, request/response APIs that exchange JSON (and, where stated, other media types) over HTTP. It covers URIs, methods, representations, errors, headers, security, versioning and documentation.

It does not cover GraphQL, gRPC, WebSocket or server-sent-event APIs, or messaging; they need their own guidance. REST over HTTP is the default only where its model fits: resources, cacheable reads, request/response.

### 0.2 How to read it

- MUST, MUST NOT, SHOULD, SHOULD NOT and MAY are used as in RFC 2119 and RFC 8174, and carry that meaning only when written in capitals.
- Every rule has an identifier (`URI-3`, `SEC-7`) so that reviews, lint rules and deviations can cite it. An identifier is never reused; a withdrawn rule keeps its number.
- A short reason, in italics, follows a rule where it isn't obvious. Where this version departs from v0.x, Appendix C says what changed and why.
- Examples use `https://api.example.org` and a stations domain. JSON in examples is valid JSON. Path templates are written with braces: `/stations/{stationId}`.

### 0.3 Principles

1. **Design for the consumer's task**, not for the database or the class model. Avoid one-to-one mappings from tables or methods to endpoints, and don't leak implementation details (technology names, internal identifiers, stack traces).
2. **Use HTTP as it is defined** (RFC 9110). Methods, status codes, headers and media types mean what the standard says. Don't tunnel one method through another, and don't invent codes.
3. **Be consistent.** The same thing is spelt, shaped and failed in the same way everywhere in an API, and across our APIs.
4. **Make change cheap.** Evolve additively, expect unknown fields, version explicitly, deprecate in the open.
5. **Be secure by default.** TLS, authentication, least privilege and bounded resource use are part of the design, not a later review.
6. **Prefer standards** (RFCs, OpenAPI, W3C Trace Context) to local inventions.

### 0.4 The short version

1. Resources are nouns: plural collections, hyphenated lowercase paths, no verbs for create/read/update/delete (§1).
2. Use each HTTP method and status code as defined, and never return `200` for an error (§2, §6).
3. JSON in and out, `lowerCamelCase` properties, RFC 3339 times, string enums and identifiers (§3).
4. Errors are RFC 9457 problem details with a `traceId`; validation errors list every problem (§7).
5. Collections are paged with cursors, sorted and filtered with flat query parameters (§5).
6. TLS 1.2 or later everywhere; credentials only in the `Authorization` header; OAuth 2.0 with PKCE for users (§10).
7. Authorise every request at object, property and function level (§10.3).
8. Put the major version in the path, change additively, deprecate with headers (§12).
9. The OpenAPI description is the source of truth: lint it, diff it, and run its examples in CI (§13).
10. Trace every request, expose health endpoints, limit what one caller can demand (§11, §14).

### 0.5 Deviations

Where a rule can't reasonably be followed, break it deliberately: record the deviation and its reason in the API's OpenAPI description (DOC-2) and take it through design review (GOV-1). A deviation applies to that API only; it doesn't change this guide. A security rule (`SEC-*`) may be deviated from only with a security review.

## 1. Resources and URIs

### 1.1 Naming

- **URI-1** Paths MUST be lowercase, and a multi-word segment MUST be hyphenated: `/train-stations`, not `/trainStations` or `/train_stations`. *Paths are case-sensitive (RFC 3986), so one spelling avoids duplicate resources and spurious `404`s.*
- **URI-2** Paths MUST NOT end in a slash or carry a file extension. The representation is chosen with `Accept`, not with `.json`.
- **URI-3** Resource names MUST be nouns. A **collection** is a plural noun (`/stations`); a **document** in it is addressed by its identifier (`/stations/waterloo`); a **singleton** that exists once per parent is a singular noun (`/stations/waterloo/profile`).
- **URI-4** Identifiers MUST be stable, opaque to clients and URL-safe. Prefer random UUIDs (version 4, RFC 9562) or other non-sequential values; version 7 UUIDs sort by creation time but reveal it, so use them only where that is acceptable. A natural key such as `waterloo` MAY be used if it is unique and never changes, and clients still treat it as opaque. Sequential database keys SHOULD NOT be exposed. *They make enumeration trivial; authorisation (SEC-10) must hold either way.*
- **URI-5** Create, read, update and delete MUST be expressed with HTTP methods, never with verbs in the path (`/getStation`, `/stations/create`).
- **URI-6** A relation that can exist only inside another resource SHOULD be nested (`/users/{userId}/messages`). Nesting SHOULD go no deeper than collection/id/collection/id; beyond that, expose the child at the top level and filter it (`/messages?userId=12`).
- **URI-7** The identity of a resource MUST be in the path. The query string only filters, sorts, pages, selects and expands (§5).
- **URI-8** *Withdrawn.* Too vague to review; §5 covers filtering, sorting and search.
- **URI-9** *Withdrawn.* Moved to §0.2 as a notation convention.

### 1.2 Actions (procedural concepts)

Prefer, in this order:

- **ACT-1** Model a state change as a change to the resource: `PATCH /stations/waterloo` with `{ "status": "closed" }`.
- **ACT-2** If it is a relationship or a flag, make it a sub-resource: `PUT /gists/{gistId}/star` and `DELETE /gists/{gistId}/star`, both idempotent.
- **ACT-3** Only when neither fits (a command that acts across resources, or computes a result), use a verb phrase as the last segment, with POST: `POST /stations/waterloo/recalculate-capacity`. The verb is an imperative in kebab-case. The operation MUST NOT be a GET.
- **ACT-4** An action across several resource types is mapped on its own: `/search`. A query too complex for a URL MAY be sent as `POST /search` with a JSON body; it is then documented as safe (it changes nothing). The HTTP `QUERY` method is designed for exactly this, but is still an Internet-Draft with limited tool support: don't use it until it is an RFC and your clients and gateways support it.

## 2. HTTP methods

| Method | Use | Safe | Idempotent | Request body |
| --- | --- | --- | --- | --- |
| GET | Retrieve a representation | yes | yes | SHOULD NOT |
| HEAD | GET without the response body | yes | yes | SHOULD NOT |
| POST | Create in a collection; run an action; submit a query | no | no (see REQ-5) | yes |
| PUT | Replace a resource at a URI the client knows | no | yes | yes |
| PATCH | Change part of a resource | no | not by definition | yes |
| DELETE | Remove a resource from its parent | no | yes | SHOULD NOT |
| OPTIONS | Say which methods a resource allows | yes | yes | no |

- **MTH-1** Methods MUST have their HTTP meaning. GET and POST MUST NOT be used to tunnel other methods (`?_method=delete`, `X-HTTP-Method-Override`), or in any way that masks or misrepresents the intent of a message.
- **MTH-2** GET and HEAD MUST be safe: no change the client could observe or be held to account for (logging and metrics aside).
- **MTH-3** A POST that creates a resource MUST answer `201 Created` with a `Location` header holding the new resource's URI, and SHOULD return its representation.
- **MTH-4** PUT replaces the whole resource and is idempotent. It MAY create a resource when the client chooses its identifier (`201` on creation, `200` or `204` on replacement). It MUST NOT be used for partial updates.
- **MTH-5** PATCH makes a partial update. The request's `Content-Type` MUST name the patch format: JSON Merge Patch (`application/merge-patch+json`, RFC 7396; a `null` clears a property, and an array is replaced whole) for plain documents, or JSON Patch (`application/json-patch+json`, RFC 6902) when arrays, precise operations or an explicit `null` value are needed. The documentation MUST list the properties that can be patched. Servers SHOULD advertise the formats they accept in `Accept-Patch`.
- **MTH-6** Updates (PUT, PATCH, and POST actions that change a resource) SHOULD return the updated representation with `200`, or `204` with no body. A client MAY choose with `Prefer: return=minimal` or `return=representation` (RFC 7240). *It saves a second round trip.*
- **MTH-7** DELETE removes the resource from its parent and answers `204` (or `200` with a body, or `202` if the removal is asynchronous). An API MUST choose one behaviour for repeating a DELETE, apply it consistently and document it: `404` (the default: it tells the client the resource is gone) or `204` (it lets a client retry after a timeout without ambiguity). Clients MUST treat `404` on a retried DELETE as success.
- **MTH-8** OPTIONS MUST answer with `Allow`. An API that browsers call MUST also answer CORS preflight requests (SEC-15). OPTIONS is not a discovery mechanism: the OpenAPI description is (DOC-1).
- **MTH-9** A method a resource doesn't support MUST be answered with `405` and `Allow`.

## 3. Representations

- **REP-1** JSON (RFC 8259, UTF-8) is the default representation: `application/json`. Every message that has a body MUST carry a `Content-Type`, and a server MUST answer an unsupported request media type with `415`. Other representations (`application/zip`, `text/csv`, `image/png`) are offered through `Accept` where they earn their place.
- **REP-2** A JSON representation MUST be an object. A bare array or scalar MUST NOT be returned at the top level: it can never be extended. Collections follow §5.
- **REP-3** A single resource is returned as itself, not inside a `data`/`meta`/`links` envelope. Status, headers and `Location` carry what a wrapper would. *v0.x both suggested a wrapper and warned against envelopes; this resolves it in favour of none.*
- **REP-4** Property names MUST be `lowerCamelCase` ASCII (`openedOn`, `stationId`). The same concept MUST have the same name everywhere. Names MUST be words, not abbreviations or type prefixes (`earnedValueChart`, not `ev`; `name`, not `strName`); the common short forms `id`, `url` and `q` are the exceptions, and an acronym is written as a word (`stationUrl`, `userId`).
- **REP-5** Enum values MUST be strings in `lowerCamelCase` (`"inService"`), never numbers. Enums are open: a client MUST cope with a value it doesn't know (treat it as "other"), and the documentation MUST say so.
- **REP-6** Instants MUST be RFC 3339 date-times with `Z` or an offset (`2026-10-04T09:30:00Z`); calendar dates are `2026-10-04`; durations are ISO 8601 (`PT15M`). An epoch number MUST NOT be used, and neither is a time without an offset for something that has already happened. An event scheduled in the future at a place (a timetabled departure) is a local date-time without offset (`2026-10-04T09:30:00`) plus its IANA zone name (`Europe/London`) in a separate property, because zone rules can change before the event; an instant alone would then be wrong. Whenever a time zone matters, send its IANA name in a separate property.
- **REP-7** Identifiers MUST be strings. An integer that can exceed 2^53 − 1 MUST be a string. Money MUST NOT be a floating-point number: use a decimal string and an ISO 4217 code (`{ "amount": "12.50", "currency": "GBP" }`). A figure that a model estimates in floating point, in a unit the model defines and with no currency (a forecast cost, say), is a measurement and not an amount of money: it MAY be a JSON number, and the documentation says what it measures and in what unit. Countries use ISO 3166-1 alpha-2, languages BCP 47.
- **REP-8** Booleans are named as questions (`isOpen`, `hasLift`). A request member that asks for a part of the result is not a boolean but an object (`"project": {}`), present when the part is wanted and absent when it is not. *A flag called `project` would not be a question, and an object can later carry settings without a breaking change.* `null` means "known, and has no value"; an absent property means "not requested, unknown or not applicable"; the documentation says which. In a response, a property cleared through JSON Merge Patch (MTH-5) is returned as `null` if it is nullable, otherwise omitted. An empty collection is `[]`, never `null`.
- **REP-9** Machine-readable values (identifiers, enums, codes) MUST NOT be localised. Human text MAY follow `Accept-Language`, and the response then carries `Content-Language`.
- **REP-10** Responses SHOULD NOT be pretty-printed. Compact JSON, compressed (HDR-9), is smaller and costs the consumer nothing; tools format it. *v0.x asked for pretty-printing by default; its own reason (bandwidth) points the other way.*
- **REP-11** Hypermedia is optional. If a representation links to other resources it SHOULD use absolute URIs, in properties named `...Url`. A client MUST NOT be forced to build a URI that the server has already made (`Location`, links).
- **REP-12** *Tolerant reader.* A client MUST ignore response properties it doesn't know. A server SHOULD reject unknown properties in a request (`422`), so that a misspelt option fails loudly instead of being ignored. The cost is that a client newer than the server (for example during a rolling deployment) fails instead of being ignored, so clients MUST NOT send a property until the server they call has released it.

## 4. Requests

- **REQ-1** Input too complex for a URL (it has types and hierarchy) MUST be sent as a JSON body with `Content-Type: application/json`. The URL carries identity, filters, paging and projection only.
- **REQ-2** Files are sent as `multipart/form-data` (the file parts, and any structured options as a part declared `application/json`) or as the request body in the file's own media type. The maximum size is documented and enforced (`413`).
- **REQ-3** A server MUST validate every request against its schema, MUST enforce limits on size, depth, length and counts, and answers with `400`, `413`, `415` or `422` as §6 describes. Client-side validation is a convenience, not a control.
- **REQ-4** *Optimistic concurrency.* A resource that can be updated concurrently SHOULD carry a strong `ETag`; PUT, PATCH and DELETE on it SHOULD require `If-Match`, answering `428` when it is missing and `412` when it no longer matches. `If-Match` uses strong comparison, so the `ETag` MUST stay strong on the wire: configure compression, gateways and CDNs not to downgrade it to a weak one (HDR-9). *This replaces "stores must support conditional PUT", which covered one method of three.*
- **REQ-5** *Idempotency.* An operation a client may need to retry after a timeout and cannot safely repeat (a POST that moves money, sends a message, starts a job) SHOULD accept an `Idempotency-Key` header: the same key with the same payload returns the first result; the same key with a different payload answers `422`; keys are scoped to the caller and expire after a documented period. *The header is an IETF Internet-Draft, but is widely implemented.*
- **REQ-6** `Accept` selects the response representation. A server MUST honour it where a resource has more than one, and SHOULD answer `406` when none is acceptable. Errors are always `application/problem+json`, whatever `Accept` says.
- **REQ-7** Clients SHOULD send a W3C `traceparent` header; servers MUST accept it, propagate it, and generate one when it is absent (OPS-2).
- **REQ-8** Servers MUST bound the size and the duration of a request (RL-5) and document the bounds.

## 5. Collections

### 5.1 The collection representation

- **COL-1** A collection response is an object with the page's items in an array named `items`, and the paging state beside it. `nextCursor` is omitted on the last page. A server MAY also send `Link` headers (RFC 8288, `rel="next"`) for generic clients. This is the representation of a collection resource. A bounded array that is a member of another representation (the `outputs` of a result, the `scenarios` of a project's listing) is a plain array under a descriptive name and is not paged; an array that can grow without bound is a collection resource instead. *v0.x preferred `Link` headers to a wrapper. In browsers they need `Access-Control-Expose-Headers`, some proxies and SDKs drop them, and they leave no room to grow; an object with `items` does, and is not an envelope around a resource.*

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

- **COL-2** Every collection that can grow MUST be paged, with a documented default and maximum page size. A server MUST NOT return an unbounded list.
- **COL-3** Paging SHOULD be cursor-based: `?limit=50&cursor=<opaque>`. A cursor is opaque to the client, survives inserts and deletes, and is validated by the server. Offset paging (`?limit=50&offset=100`) MAY be used for small, slow-changing collections only: it skips and repeats items under change, and slows with depth.
- **COL-4** A total count is expensive on large collections. Offer it only on request (`include=totalCount`), or as an estimate, and say which.
- **COL-5** `sort` takes a comma-separated list of property names; a leading `-` means descending: `?sort=-openedOn,name`. The default order MUST be documented and total (tie-broken by identifier), or paging isn't stable.
- **COL-6** Filters are flat parameters named for the property: `?status=open&lineId=7`. Ranges use `...After`/`...Before` or `...Min`/`...Max` (`openedAfter=2026-01-01`); free-text search is `q`. The documentation says whether a match is exact, prefix or contains. Bracketed parameters (`filter[status]`, `page[offset]`, `fields[station]`) SHOULD NOT be used: brackets must be percent-encoded (RFC 3986), many frameworks and gateways don't bind them, and flat names read as well.
- **COL-7** `fields=id,name` limits the properties returned (the identifier is always returned).
- **COL-8** `include=lines,customer` adds related resources, or optional parts of the representation that aren't returned by default because they're costly or rarely wanted. It lets a client get what it needs in one request.
- **COL-9** An unknown query parameter, field name or `include` value MUST be answered with `400`. *A misspelt filter must not quietly return everything.*
- **COL-10** *Withdrawn.* Normalising parameter order for caching is the job of the cache or CDN, not of clients.
- **COL-11** Aliases MAY be offered for frequent, stable queries (`/users/me`, `/orders?status=open`); they are documented and don't change meaning.

## 6. Responses and status codes

- **STS-1** The status code describes the outcome of the request as a whole. `200` MUST NOT be used to carry an error in the body.
- **STS-2** Use the most specific code the standard offers; never invent one. Appendix B lists the codes this guide uses.
- **STS-3** Successes: `200` for success with a body; `201` for creation (MTH-3); `202` when the work has been accepted but isn't done (§9); `204` when the body is intentionally empty.
- **STS-4** Redirection is for relocated resources: `308` (permanent) and `307` (temporary) keep the method and body; `301` and `302` MUST NOT be used, because clients may turn a POST into a GET. Two other codes have their own jobs: `303` sends a client to the result of an operation, and `304` answers a conditional GET.
- **STS-5** `400` means the request can't be understood: malformed syntax or framing, an invalid parameter. `422` means it was understood and is well-formed, but breaks the rules of the schema or the domain. `409` means it conflicts with the current state (a duplicate, a stale version).
- **STS-6** `401` means the credentials are missing or wrong, and MUST carry `WWW-Authenticate`. `403` means the caller is known and not permitted. Where saying that a resource exists would itself disclose something, answer `404` instead of `403`.
- **STS-7** `404` when a URI maps to no resource. `410` when it is gone for good. `412`, `413`, `415`, `428` and `429` as Appendix B says.
- **STS-8** `500` means the API malfunctioned; `501` that it doesn't support what was asked; `502` and `504` that an upstream failed or timed out; `503` that it is overloaded or down for maintenance, or that a request overran its processing limit (RL-5), with `Retry-After`. With several problems of one kind, use the most general code that fits (`400`, `500`).
- **STS-9** An operation is atomic where it can be. If part of one operation fails, the response reports the failure with the status that fits its cause; partial results MAY be included as documented extension members. A success-class status reports individual failures only for batches (BLK-2).

## 7. Errors

- **ERR-1** Every `4xx` and `5xx` response MUST be a problem details object (RFC 9457) with `Content-Type: application/problem+json`, including those produced by frameworks and authentication (`401`, `404`, `405`). Errors produced by a gateway or other infrastructure SHOULD be configured to do the same; where that isn't possible, the exception is documented (§0.5). A client MUST NOT have to handle a second error format from the API's own code. *RFC 9457 obsoletes RFC 7807. It replaces the JSON:API-style `errors` object of v0.x with a standard that generic clients and frameworks already understand.*
- **ERR-2** The standard members are used as follows: `type`, a stable, documented URI that identifies the kind of problem (`about:blank` only when the status says all there is to say); `title`, a short summary that is the same for every occurrence of the `type`; `status`, the HTTP status as a number; `detail`, an explanation of this occurrence, for people; and `instance`, a URI that identifies this occurrence (for example `urn:uuid:...`).
- **ERR-3** Every problem MUST carry `traceId`, the trace identifier of the request (HDR-8), so that a report can be matched to the logs.
- **ERR-4** A problem about the request's content MUST list every problem found, not only the first, in an `errors` array. Each entry has `detail` and a location: `pointer`, a JSON Pointer (RFC 6901) into the request body in URI-fragment form (`#/address/postcode`, as in RFC 9457's own example); or `parameter` (a query or path parameter); or `header`. An entry MAY have `code`, a short stable identifier for the rule broken (`required`, `outOfRange`). Body validation is `422`; a bad parameter or malformed syntax is `400`.
- **ERR-5** With several problems in one request, the status is the most generally applicable one (STS-8). A request with both body problems (`422`) and parameter or syntax problems (`400`) gets `400`, and the `errors` array still lists them all.
- **ERR-6** Errors MUST NOT leak implementation: no stack traces, SQL, class names, file paths or host names, and nothing that tells an attacker whether an account exists. The cause of a `500` goes to the log, under the `traceId`.
- **ERR-7** `type` and `code` are for programs and are never localised. `title` and `detail` are for people, MAY follow `Accept-Language`, and MUST NOT be parsed by clients. A client that doesn't know a `type` treats the problem as its status class.
- **ERR-8** Every `type` an API can return is documented, with an example, in its OpenAPI description (DOC-2).
- **ERR-9** The headers that must accompany particular error statuses (`WWW-Authenticate`, `Allow`, `Retry-After`) are in HDR-6.

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

- **HDR-1** `Content-Type` MUST be set on every message with a body. JSON needs no `charset` (it is UTF-8). `Content-Length`, `Transfer-Encoding` and `Date` belong to the server and the protocol stack, not to API code. *v0.x asked for `Content-Length`; with chunked transfer and HTTP/2 and HTTP/3 framing it is no longer an API-level rule.*
- **HDR-2** `Location` MUST accompany `201`, `202` (the status resource) and redirects.
- **HDR-3** Cacheable representations SHOULD carry a strong `ETag` (and MAY carry `Last-Modified`). A conditional GET with `If-None-Match` or `If-Modified-Since` answers `304` when nothing has changed, to save bandwidth.
- **HDR-4** `Cache-Control` (RFC 9111) is how caching is stated. `Expires` and `Pragma` SHOULD NOT be sent: they are superseded. Reference data that changes rarely: `public, max-age=<seconds>`, with `stale-while-revalidate` where staleness is tolerable. Data that belongs to the caller: `private`. Personal or sensitive data: `no-store` (`no-cache` means "revalidate before reuse", not "don't store"). Caching SHOULD be encouraged wherever a GET can safely be cached; freshness headers belong on `200`, and MAY be on `308`, `404` and `410`.
- **HDR-5** `Vary` MUST list every request header that changes the representation (`Accept`, `Accept-Language`, `Accept-Encoding`).
- **HDR-6** `Allow` MUST accompany `405`; `WWW-Authenticate` MUST accompany `401`; `Retry-After` MUST accompany `429` and `503`; `Accept-Patch` SHOULD accompany resources that can be patched.
- **HDR-7** A custom header MUST NOT change the behaviour of an HTTP method or replace a standard header. A new one MUST NOT begin `X-` (RFC 6648), SHOULD be descriptive (`Request-Id`) and MUST be documented in the OpenAPI description. Prefer the standard header where there is one (`Authorization`, `traceparent`, `Retry-After`, `Idempotency-Key`).
- **HDR-8** Every response SHOULD carry a `Request-Id`: the trace identifier of the request (32 hexadecimal characters), the same value that appears in error bodies (`traceId`) and in the server's logs.
- **HDR-9** Servers SHOULD compress responses for clients that accept it (`gzip`, `br`; `zstd` where supported) and MUST set `Vary: Accept-Encoding`. Already-compressed media (images, archives) aren't compressed again.
- **HDR-10** A deprecated operation carries `Deprecation` (RFC 9745) and, once removal is scheduled, `Sunset` (RFC 8594), with `Link` relations `deprecation` and `sunset` pointing at the migration guide (VER-5).
- **HDR-11** Security headers are in SEC-15 and SEC-16.

## 9. Long-running and bulk operations

- **ASY-1** An operation that can't be relied on to finish within a few seconds SHOULD be asynchronous: answer `202 Accepted` with a `Location` header for a status resource and `Retry-After` as a hint. An operation MAY stay synchronous when it has a short, documented upper bound; past that bound it is `202`. A service that keeps nothing between requests (SEC-9), and so has nowhere to keep a job, MAY instead stop the operation at the bound and answer as RL-5 says; it records that choice in its OpenAPI description (§0.5).
- **ASY-2** The status resource (`GET /operations/{operationId}`) reports a state (`running`, `succeeded`, `failed`, `cancelled`), MAY report progress, and on success links to the result (or redirects to it with `303`); on failure it embeds a problem (§7). Clients MUST NOT poll faster than `Retry-After`.
- **ASY-3** A running operation is cancelled with `POST /operations/{operationId}/cancel` (ACT-3), after which the status resource reports `cancelled`; a status resource is kept for a documented time. A server MAY also offer signed webhooks; a webhook is a notification, never the only way to learn the outcome.
- **BLK-1** Prefer one resource per request. Offer a batch operation only where round trips are the problem, and document its maximum size.
- **BLK-2** A batch SHOULD be atomic. Where it can't be, answer `207 Multi-Status` with an `items` array: one entry per input, in order, each with its own `status` and either the result or a problem. This is the one place a success-class status reports individual failures.

## 10. Security

### 10.1 Transport

- **SEC-1** TLS MUST be used on every endpoint that can be reached from beyond the machine it runs on. ("TLS everywhere": SSL is obsolete.) Such an endpoint MUST NOT also serve plain HTTP, not even to redirect: a client that has sent a credential over HTTP has already exposed it. An endpoint that only local programs reach (loopback, a Unix domain socket) MAY use plain HTTP. TLS MAY be terminated at a load balancer, ingress or service-mesh proxy, with plain HTTP only on the last hop inside a trusted, access-controlled network (including probes from the orchestrator); that arrangement is documented. Certificate issuance MUST NOT need a plain-HTTP listener on the API host (use DNS-01 or a dedicated validation endpoint).
- **SEC-2** TLS 1.2 is the minimum and TLS 1.3 SHOULD be preferred; TLS 1.0 and 1.1 MUST be disabled (RFC 8996). Cipher suites follow RFC 9325: authenticated encryption with forward secrecy (ECDHE) only, which TLS 1.3 gives by default. Certificates come from a trusted authority, are renewed automatically, and are watched with CAA records and Certificate Transparency monitoring.
- **SEC-3** A host that browsers can reach SHOULD send `Strict-Transport-Security` (RFC 6797) with a `max-age` of at least a year and `includeSubDomains`. It MUST NOT be sent on `localhost` or development hosts, where a browser would remember it.
- **SEC-4** HTTP Public Key Pinning MUST NOT be used (browsers have withdrawn it). A native client MAY pin only with a reviewed plan for rotation and recovery.
- **SEC-5** Service-to-service calls SHOULD use mutual TLS or sender-constrained tokens (SEC-7) where the data warrants it.

### 10.2 Authentication

- **SEC-6** Credentials, tokens and API keys MUST NOT appear in a URL, in the path or the query: URLs are logged, kept in histories and sent in `Referer`. They go in the standard `Authorization` header (not a custom one); a body carries a secret only at a dedicated token endpoint. Sensitive personal data (government identifiers, health or financial data, special-category data) SHOULD NOT appear in a URL either; look such a resource up with a body, as in ACT-4. Ordinary identifiers and filters (`/users/{userId}`, `?lineId=7`) are fine.
- **SEC-7** For access on behalf of a user, use OAuth 2.0 with OpenID Connect: the authorization-code flow with PKCE (RFC 7636). For service-to-service access, use the client-credentials flow. The implicit flow and the resource-owner password flow MUST NOT be used (RFC 9700 forbids the second and advises against the first; the OAuth 2.1 draft removes both). Access tokens SHOULD be short-lived (an hour or less) and refresh tokens rotated. A JWT access token MUST be fully validated (RFC 9068): its signature, against an allow-list of algorithms (never `none`), and `iss`, `aud`, `exp` and `nbf`, with keys fetched from the issuer's JWKS and rotated. High-risk APIs SHOULD bind tokens to the sender (DPoP, RFC 9449, or mutual TLS, RFC 8705).
- **SEC-8** A static API key MAY protect low-risk, server-to-server access. It MUST have at least 128 bits of entropy, be stored only as a hash, be compared in constant time, be scoped, revocable and rotatable, and travel as `Authorization: Bearer <key>`.
- **SEC-9** An API MUST be stateless: no server-side session is needed to process a request. Cookies MAY be used for browser clients only, as `Secure; HttpOnly; SameSite`, with protection against cross-site request forgery.

### 10.3 Authorisation and data

- **SEC-10** The server MUST authorise every request, except those documented as public (CORS preflight, which browsers send without credentials; the health endpoints of OPS-1; the OpenAPI description and a JWKS, if served), at three levels: the **object** (may this caller touch this identifier? OWASP API1), the **property** (may they read or write this field? API3; writable properties are an allow-list, which prevents mass assignment) and the **function** (may they call this operation? API5). Roles and scopes are least privilege.
- **SEC-11** The server MUST NOT trust client-supplied identity, roles, prices or other values it can derive; it derives them from the token and its own data.
- **SEC-12** Responses MUST return only what the caller needs. Sensitive data MUST be classified, personal data MUST be handled under the privacy policy, and neither MAY be written to logs.
- **SEC-13** The status for unauthenticated and unauthorised callers is set by STS-6. A failed sign-in MUST NOT say which part was wrong.

### 10.4 Input and resources

- **SEC-14** Input is validated against a schema (types, ranges, formats, enums), unknown properties are rejected, and size, depth, array length and string length are limited. Data stores are reached through parameterised queries or safe APIs. A server MUST NOT fetch a caller-supplied URL without defences against server-side request forgery (API7): an allow-list where the destinations are known, and otherwise HTTPS only, resolution checks that block loopback, link-local and private ranges, no redirects followed, and egress filtering. This covers webhook destinations (ASY-3). Uploaded files are checked for type and size and stored away from anything executable.
- **SEC-15** CORS is enabled only for APIs that browsers call, with an explicit allow-list of origins, the fewest methods and headers needed, and a preflight cache lifetime. `Access-Control-Allow-Origin: *` MUST NOT be combined with credentials.
- **SEC-16** Every response carries the right `Content-Type` and `X-Content-Type-Options: nosniff`. A response containing personal or sensitive data carries `Cache-Control: no-store`. An API whose responses could be framed or rendered MAY add `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`.

### 10.5 Operating securely

- **SEC-17** Authentication events, authorisation failures and validation spikes MUST be logged with the trace identifier, and SHOULD be alerted on; secrets, tokens and personal data MUST NOT be logged; privileged actions MUST be auditable.
- **SEC-18** Every API, version and endpoint MUST be in an inventory; versions past their sunset date MUST be retired (API9); there MUST be no undocumented endpoints; dependencies MUST be scanned and patched; secrets MUST live in a secrets manager, never in source or in images.
- **SEC-19** Responses from other APIs we call are untrusted input: they MUST be validated, called with timeouts, and fetched over TLS (API10).
- **SEC-20** The OWASP API Security Top 10 (2023) is the minimum threat checklist; each release MUST be reviewed against it (Appendix A).

## 11. Rate limiting and resource protection

- **RL-1** Every API MUST limit the work one caller can demand (rates, quotas, concurrency) and MUST document the limits (OWASP API4).
- **RL-2** A caller over a limit gets `429 Too Many Requests`. A server that is overloaded or draining, whoever is calling, answers `503`. Both carry `Retry-After` (seconds or an HTTP date).
- **RL-3** Responses SHOULD carry the rate-limit state in return headers, so that callers can see the rules: the IETF `RateLimit-Policy` and `RateLimit` fields. They are still an Internet-Draft (draft-ietf-httpapi-ratelimit-headers) and have changed between drafts; until they are an RFC, document exactly which fields the API sends. The older `X-RateLimit-Limit`, `X-RateLimit-Remaining` and `X-RateLimit-Reset` are common but not a standard.
- **RL-4** Clients MUST honour `Retry-After`, and back off exponentially with jitter on `429`, `503` and `504`. They retry only requests that are idempotent or carry an `Idempotency-Key`, and they don't retry a problem whose documented `type` says that a retry won't help (RL-5).
- **RL-5** Limits on the size and the duration of a request MUST be documented, and enforced with `413`, `414`, `431` and `408` (the client was too slow to send it). A request that exceeds the server's own processing limit gets `503` with `Retry-After`, or becomes asynchronous (ASY-1); `504` is for an upstream that timed out. Because clients retry a `503` (RL-4), a server that can tell that the request is too large for the limit, however idle the server is, SHOULD answer `422` instead, with a problem `type` that says so. Where it can't tell load from size, it answers `503`, and the problem `type` says that the limit was exceeded and that a retry may take as long again. *A retry of work that is too large only costs the server the same limit again.*

## 12. Versioning and evolution

- **VER-1** Every API MUST be versioned from its first release. The major version is the first path segment (`/v1/stations`), visible in logs, caches and `curl`. It MUST NOT be carried in the query string, and SHOULD NOT be carried in a media type. A header-selected date version (as Stripe does) MAY be added by a large public API; it isn't the default.
- **VER-2** Within a major version, changes MUST be additive. Compatible changes: new endpoints, new optional request properties and parameters, new response properties, new enum values (REP-5), new optional headers, new problem `type`s, looser validation.
- **VER-3** Breaking changes, which need a new major version: removing or renaming anything; changing a type, unit, format or meaning; making something optional required; tighter validation; changing a status code or problem `type` for an existing case; changing a default order or page size; removing an enum value; changing authentication.
- **VER-4** Consumers MUST tolerate the additive changes of VER-2 (REP-12).
- **VER-5** A version or an operation is deprecated in the open: announced with a migration guide, marked with `Deprecation` and `Sunset` headers (HDR-10), served side by side with its replacement for the notice period, watched through per-version usage metrics, and removed on the stated date. The minimum notice is set by audience: three months for internal consumers, twelve for external ones.
- **VER-6** The OpenAPI description carries the full semantic version (`info.version`); the path carries only the major version.

## 13. Documentation, testing and governance

- **DOC-1** Every API MUST have an OpenAPI description (version 3.1 or later; 3.2 is current) as its single source of truth. It lives in source control beside the code, changes with it, and is published where consumers find it: served by the API, or on a developer portal. ("Swagger" is the old name of the specification; Swagger UI and Swagger Editor are tools that read it.)
- **DOC-2** The description MUST cover every operation, parameter and schema, every status code returned and every problem `type` (with an example), the security schemes, the rate limits, and any deviation from this guide (§0.5). Each operation MUST have an example of a complete request/response cycle, and SHOULD have one for an error that is specific to it; the common errors are shown once, in the guide.
- **DOC-3** CI MUST lint the description against this guide (for example with a Spectral ruleset). For an API with consumers outside the owning team, CI MUST also compare it with the last release for breaking changes (for example with oasdiff); for others that is a SHOULD. Contract tests generated from it SHOULD run against the implementation, so that the document and the code can't drift apart.
- **DOC-4** Reference documentation SHOULD be generated from the description (Redoc, Scalar, Swagger UI), with written guides for getting started, authentication, errors, paging and the changelog. Examples in the documentation SHOULD be executed in CI, not typed by hand.
- **DOC-5** Every release that changes the API MUST have a changelog entry, and a migration guide if it deprecates something.
- **GOV-1** A new API, and any breaking change, goes through design review against Appendix A before it is built. Deviations are recorded (§0.5).
- **GOV-2** This guide is versioned and its changes are recorded (Appendix C).

## 14. Operations

- **OPS-1** A service SHOULD expose `GET /health/live` (the process is up) and `GET /health/ready` (it can take traffic; `503` until it can). They aren't versioned, need no credentials, carry `Cache-Control: no-store`, and return no internals. Detailed diagnostics MUST be authenticated.
- **OPS-2** Servers MUST accept, propagate and generate W3C Trace Context (`traceparent`, `tracestate`). Logs MUST be structured and carry the trace identifier.
- **OPS-3** Metrics (rate, errors, duration) MUST be recorded per route template (`/stations/{stationId}`), not per URL, and per API version. Each API MUST have stated availability and latency objectives.
- **OPS-4** Servers SHOULD stop gracefully: they stop accepting work, let in-flight requests finish within a documented time, and exit.

## Appendix A. Design review checklist

| Check | Rules |
| --- | --- |
| Resources are nouns; collections plural; paths lowercase and hyphenated, with no verbs or extensions | URI-1 to URI-7 |
| Actions are a state change or a sub-resource before they are a verb; complex queries are safe POSTs | ACT-1 to ACT-4 |
| Each method means what HTTP says; creation is `201` with `Location` | MTH-1 to MTH-9 |
| Representations are objects in `lowerCamelCase`; enums are strings; times are RFC 3339 | REP-1 to REP-12 |
| Concurrency and idempotency are decided for every unsafe operation | REQ-4, REQ-5 |
| Every collection is paged, deterministically sorted, and filtered with flat parameters | COL-1 to COL-11 |
| Status codes are right and no error hides in a `200` | STS-1 to STS-9 |
| Every error is problem details with a `traceId`; validation errors list every problem | ERR-1 to ERR-9 |
| Caching is decided per resource; `ETag` where it helps | HDR-3 to HDR-5 |
| Long operations are `202` with a status resource, or bounded and documented; batches are bounded | ASY-1 to ASY-3, BLK-1, BLK-2 |
| TLS 1.2 or later; no credentials in URLs; the standard `Authorization` header | SEC-1 to SEC-8 |
| Authorisation is checked per object, property and function, and tested with another caller's identifiers | SEC-10, SEC-11 |
| Input is validated and bounded; unknown properties are rejected | REQ-3, SEC-14 |
| The OWASP API Security Top 10 has been reviewed | SEC-20 |
| Limits are set and documented; `429` or `503` with `Retry-After` | RL-1 to RL-5 |
| Versioned in the path; no breaking change inside a major version | VER-1 to VER-4 |
| The OpenAPI description is complete, linted, diffed and has examples | DOC-1 to DOC-4 |
| Health, tracing, logging and graceful shutdown are in place | OPS-1 to OPS-4, SEC-17 |
| Deviations are recorded | §0.5 |

## Appendix B. Status codes

| Code | Name | Use |
| --- | --- | --- |
| 200 | OK | Success with a body. Never carries an error. |
| 201 | Created | A resource was created; `Location`; the body SHOULD be its representation. |
| 202 | Accepted | The work was accepted and isn't finished; `Location` of the status resource. |
| 204 | No Content | Success; the body is intentionally empty. |
| 207 | Multi-Status | A batch with an outcome per item (BLK-2). |
| 303 | See Other | Go and GET the result of an operation. |
| 304 | Not Modified | A conditional GET found nothing changed. |
| 307 | Temporary Redirect | Temporarily elsewhere; the method is kept. |
| 308 | Permanent Redirect | Permanently elsewhere; the method is kept. |
| 400 | Bad Request | The request can't be understood: malformed, or an invalid parameter. |
| 401 | Unauthorized | Credentials missing or invalid; `WWW-Authenticate`. |
| 403 | Forbidden | The caller is known and not permitted. |
| 404 | Not Found | No resource at the URI, or one that mustn't be disclosed. |
| 405 | Method Not Allowed | The method isn't supported here; `Allow`. |
| 406 | Not Acceptable | None of the representations in `Accept` is available. |
| 408 | Request Timeout | The client took too long to send the request. |
| 409 | Conflict | The request conflicts with the current state. |
| 410 | Gone | Removed for good. |
| 412 | Precondition Failed | `If-Match` (or another precondition) no longer holds. |
| 413 | Content Too Large | The request body exceeds the limit. |
| 414 | URI Too Long | The URI exceeds the limit. |
| 415 | Unsupported Media Type | The request's `Content-Type` (or patch format) isn't supported. |
| 422 | Unprocessable Content | Well-formed, but breaks the rules of the schema or the domain, or is too large for a limit (RL-5). |
| 428 | Precondition Required | `If-Match` is required and missing. |
| 429 | Too Many Requests | The caller is over a limit; `Retry-After`. |
| 431 | Request Header Fields Too Large | The headers exceed the limit. |
| 500 | Internal Server Error | The API malfunctioned. |
| 501 | Not Implemented | The API doesn't support what was asked. |
| 502 | Bad Gateway | An upstream returned an invalid response. |
| 503 | Service Unavailable | Overloaded, in maintenance, or a request that overran the server's processing limit (RL-5); `Retry-After`. |
| 504 | Gateway Timeout | An upstream timed out. |

`301` and `302` MUST NOT be used (STS-4).

## Appendix C. Changes from v0.x

Kind: **C** a correction (a fact or a defect), **M** a modernisation (a choice), **N** new.

| # | v0.x | v1.1 | Why | Kind |
| --- | --- | --- | --- | --- |
| 1 | No definition of "must", "should", "may"; no rule identifiers | RFC 2119/8174 keywords, rule identifiers, reasons, a deviations process | Rules can be cited, linted and reviewed | N |
| 2 | "A singular noun for object instances" (`/stations/waterloo`) | Collection, document and singleton named properly; identifiers stable, opaque and non-sequential | `waterloo` is an identifier, not a noun to be chosen | M |
| 3 | "Lowercase should be preferred" | Lowercase MUST; hyphenated segments; no extensions or trailing slash | One spelling per resource | M |
| 4 | Nested relations inline | Nesting depth limited | Deep paths couple clients to the hierarchy | M |
| 5 | Verbs for procedural concepts | State change first, then sub-resource, then verb | Fewer RPC-style endpoints | M |
| 6 | PUT "inserts and updates", and again "updates mutable resources" | PUT replaces; PATCH (JSON Merge Patch or JSON Patch) is the partial update | PATCH appeared in an example but was never defined | C |
| 7 | OPTIONS "should be used to retrieve metadata" | OPTIONS answers `Allow` and CORS preflight; OpenAPI is the discovery mechanism | In practice OPTIONS is preflight | M |
| 8 | `:id` in paths | `{id}` | OpenAPI's notation | C |
| 9 | JSON input | Kept; `multipart/form-data` for files | Uploads were not covered | N |
| 10 | Pagination by `Link` header; `Total-Count` header | `items` array and `nextCursor` in the body, cursor paging by default, total only on request | Headers are awkward for browsers, proxies and SDKs; offsets drift | M |
| 11 | `page[offset]`, `filter[field]`, `fields[resource]` | Flat, `lowerCamelCase` parameters | Brackets need percent-encoding and bind badly | M |
| 12 | `include`, `fields`, `sort` | Kept; unknown parameters are `400` | A typo must not return everything | M |
| 13 | "Include rate limiting information in return headers" | `429`/`503` with `Retry-After`; `RateLimit` fields flagged as a draft | The code to use was missing | M |
| 14 | An optional `data`/`errors`/`meta`/`links` wrapper, and also "avoid envelopes" | No wrapper; collections are an object with `items` | The two rules contradicted each other | M |
| 15 | `errors` objects with string `status`, `code`, `links`, `paths` | RFC 9457 problem details; `errors[]` entries with `pointer`, `code`, `detail`; numeric `status` | A standard instead of a house format; one pointer, not two | M |
| 16 | JSON examples with typographic quotes and a missing bracket | Valid JSON | The examples did not parse | C |
| 17 | A PATCH body in the JSON:API shape | Merge Patch and JSON Patch | Standard patch formats | M |
| 18 | `Content-Length` "should be used" | Not an API-level rule | Chunking, HTTP/2 and HTTP/3 | C |
| 19 | `Last-Modified` and `ETag` "should be used" | Strong `ETag` SHOULD; `Last-Modified` MAY; `304` | `ETag` is exact; `Last-Modified` has one-second resolution | M |
| 20 | "Stores must support conditional PUT" | `If-Match` on PUT, PATCH and DELETE; `412`, `428` | Lost updates happen with all three | M |
| 21 | `Cache-Control`, `Expires` and `Pragma` | `Cache-Control` and `Vary` | `Expires` and `Pragma` are superseded | C |
| 22 | Custom headers must not change method behaviour | Kept; no `X-` prefix; documented | RFC 6648 | M |
| 23 | (absent) | `Request-Id`, `traceparent`, `Deprecation`, `Sunset`, compression, `Idempotency-Key` | Operability and lifecycle | N |
| 24 | `301` "to relocate"; `302` "should not be used" | `308`/`307`; `303`; `301` and `302` MUST NOT be used | `301` and `302` let clients change the method | C |
| 25 | `408` listed without a description; no `409`, `410`, `413`, `415`, `428`, `429`, `502`, `503`, `504` | Appendix B | Codes the rules already relied on | C |
| 26 | `400` "may be used to indicate nonspecific failure" | `400` malformed; `422` invalid; `409` conflict | A usable split | M |
| 27 | "SSL everywhere" | "TLS everywhere"; no plain-HTTP twin for a remote endpoint | SSL is obsolete | C |
| 28 | "TLS 1.1 (or greater)" | TLS 1.2 minimum, 1.3 preferred; 1.0 and 1.1 disabled | RFC 8996 deprecated them in 2021 | C |
| 29 | "Public key pinning where possible" | Removed | Browsers withdrew HPKP | C |
| 30 | "Perfect Forward Secrecy where possible" | Required: ECDHE or TLS 1.3 | It is the default now | C |
| 31 | "HTST where possible" | HSTS, for browser-facing hosts, never `localhost` | Typo; scope | C |
| 32 | "Authentication in a custom header" | The standard `Authorization` header; nothing secret in URLs | Standard headers are understood by every client and log filter | C |
| 33 | "No cookies or sessions" | Stateless; cookies only for browser clients, with flags and CSRF protection | Browser-based clients exist | M |
| 34 | "OAuth 2.0 when a token isn't secure enough" | Which flows, token lifetimes, JWT validation, sender-constrained tokens; implicit and password flows banned | RFC 9700, RFC 9068 | C |
| 35 | (absent) | Authorisation at three levels, input validation, SSRF, CORS, security headers, logging, inventory, OWASP API Security Top 10 | The usual causes of breaches | N |
| 36 | "Swagger documentation is preferable" | OpenAPI 3.1 or later as the source of truth; lint, diff and contract tests; examples run in CI | Documentation that cannot drift | M |
| 37 | Versioning links; a date-header option | URL major version; additive-only policy; breaking changes defined; deprecation headers; notice periods; stale links removed | A policy, not a reading list | M |
| 38 | "Pretty print by default and support gzip" | Compact JSON, compressed | Pretty-printing costs bytes | C |
| 39 | "Avoid envelopes unless the client can't use headers (JSONP)" | JSONP is obsolete (use CORS); see rows 10 and 14 | JSONP is gone | C |
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

Change one property, if nobody else has changed the station:

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

A page of open stations, two at a time, by name:

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
- OpenAPI Specification 3.1 and 3.2 (3.2.0 was released in September 2025); JSON Schema 2020-12
- W3C Trace Context
- OWASP API Security Top 10 (2023) and the OWASP REST Security Cheat Sheet

**Internet-Drafts** (status as of October 2026; check the IETF datatracker before relying on them)

- `draft-ietf-httpapi-ratelimit-headers`: the `RateLimit` and `RateLimit-Policy` fields
- `draft-ietf-httpapi-idempotency-key-header`: the `Idempotency-Key` field
- `draft-ietf-httpbis-safe-method-w-body`: the `QUERY` method

**Other guidance consulted**

- Microsoft REST API Guidelines; Google API Improvement Proposals; Zalando RESTful API Guidelines
- jsonapi.org: the basis of v0.x's wrapper and bracketed parameters, which this version does not adopt
- M. Masse, *REST API Design Rulebook* (O'Reilly, 2011): the origin of the URI naming rules; older than most of the standards above

## Appendix F. Revision history

| Version | Date | Changes |
| --- | --- | --- |
| 1.0 (draft) | 4 October 2026 | The first revision since v0.x; Appendix C lists every change |
| 1.1 | 4 October 2026 | The owner's review of the draft: stricter MUSTs for traceability, security and operations (ERR-3, HDR-6, SEC-12, SEC-17 to SEC-20, OPS-2, OPS-3); an allowance for TLS that ends at a proxy (SEC-1); a request over the server's own limit is `503` and `504` is for an upstream (RL-5); clearer documentation duties (DOC-2 to DOC-5); URI-8, URI-9 and COL-10 withdrawn. Clarified by applying the guide to an API: a request too large for a limit is `422` and load is `503` (RL-4, RL-5, STS-8, Appendix B); an estimate need not be a decimal string (REP-7); a request member that asks for a part of the result is an object (REP-8); a bounded array inside a representation is not a collection (COL-1); a service that keeps nothing may stop at its bound instead of answering `202` (ASY-1) |

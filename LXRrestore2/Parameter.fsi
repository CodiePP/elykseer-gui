namespace LXRrestore
open System

module Parameter = 

    val stringOrDefault<'a> : string -> 'a -> string
    val intOrDefault<'a> : string -> 'a -> int

    val setParameter<'a> : string -> 'a -> unit

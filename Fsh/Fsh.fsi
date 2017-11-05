namespace Fsh

open System
open Xwt

module FshButton =

    (*val create : string -> Xwt.Button*)
    val createWithHandler : string -> (Xwt.Button -> EventArgs -> unit) -> Xwt.Button
    val imageWithHandler : string -> string -> (Xwt.Button -> EventArgs -> unit) -> Xwt.Button

namespace Fsh

open System
open Xwt

module FshButton =

    let create (lbl : string) =
        let b = new Xwt.Button(lbl)
        b

    let createWithHandler (lbl : string) (hdlr : Xwt.Button -> EventArgs -> unit) =
        let b = new Xwt.Button(lbl)
        b.Clicked.Add(hdlr b)
        b


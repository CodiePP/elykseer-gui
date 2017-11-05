namespace Fsh

open System
open Xwt
open LXRgui

module FshButton =

(*    let create (lbl : string) =
        let b = new Xwt.Button(lbl)
        b.Font <- Xwt.Drawing.Font.SystemFont.WithSize(16.0).WithWeight(Drawing.FontWeight.Bold)
        b
*)

    let createWithHandler (lbl : string) (hdlr : Xwt.Button -> EventArgs -> unit) =
        let b = new Xwt.Button(lbl)
        //Console.WriteLine("new button with label '{0}'", lbl)
        //b.Font <- Xwt.Drawing.Font.SystemFont.WithSize(16.0).WithWeight(Drawing.FontWeight.Bold)
        //b.BackgroundColor <- Coloring.ink
        b.Clicked.Add(hdlr b)
        b

    let imageWithHandler (lbl : string) (fname : string) (hdlr : Xwt.Button -> EventArgs -> unit) =
        //Console.WriteLine("loading image {0}", fname)
        let img = Xwt.Drawing.Image.FromResource(fname)
        //Console.WriteLine("new button with image {0}", img)
        let b = new Xwt.Button(img (*, lbl*))
        b.Clicked.Add(hdlr b)
        b

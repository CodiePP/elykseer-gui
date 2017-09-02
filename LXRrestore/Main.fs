namespace LXRrestore

module Main = 
    open System
    open Xwt

    [<EntryPoint>]
    let Main(args) = 
        Application.Initialize(ToolkitType.Gtk)

        // init and run GUI
        let w = ThisApplication.initialize ()
        Application.Run()
        0

namespace LXRbackup

module MainWindow = 
    open System
    open Xwt

    type Window() as this = 
        inherit Xwt.Window()
        do this.Title <- "LXRbackup"
        do this.Size <- new Size(600.0, 720.0)
        do this.Show()
        do this.Closed.Add (fun e -> Application.Exit())

namespace LXRbackup

module ThisApplication = 
    open System
    open System.Threading
    open System.Threading.Tasks
    open System.IO
    open Xwt
    open Xwt.Drawing
    open Fsh
    open LXRgui

   // events which control control flow
    let stage = new Event<int>()
    let reached_stage = stage.Publish

    // event which populate list of filepaths
    let filepath = new Event<string>()
    let enter_filepath = filepath.Publish

    // event which shows text in statusbar
    //let statbar = new Event<string>()
    //let show_statbar = statbar.Publish
    //let publish_statbar (m : string) = statbar.Trigger(m)

    let mutable optN = Parameter.intOrDefault "nchunks"  256
    let mutable optComp = Parameter.intOrDefault "compression" 0
    let mutable optDedup = Parameter.intOrDefault "deduplication" 0
    let mutable optRed = Parameter.intOrDefault "redundancy" 0
    let mutable optOutP = Parameter.stringOrDefault "outputdir" "/tmp/LXR"
    let mutable optDatP = Parameter.stringOrDefault "dbdir" "/tmp/meta"

    let mutable optPath = ""

    // the data model
    let df1 = new DataField<string>()
    let dta = new ListStore(df1)

    let addFile fn = 
        // must be a file
        if SBCLab.LXR.FileCtrl.fileExists fn then
            let fi = new FileInfo(fn)
            if fi.Attributes.HasFlag(FileAttributes.Normal) then
                filepath.Trigger("F::" + fn)
                stage.Trigger(2)
                //Console.WriteLine("file attributes {0}", fi.Attributes.ToString())
            else
                Console.WriteLine("skipping: " + fn)
        ()

    let addDir1 fn = 
        SBCLab.LXR.Logging.log () <| Printf.sprintf "addDir1 %A" fn
        // must be a directory
        // only first depth files are added
        if SBCLab.LXR.FileCtrl.dirExists fn then
            let dirinfo = new DirectoryInfo(fn) in
            if dirinfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
                || dirinfo.Attributes.HasFlag(FileAttributes.System) then
                Console.WriteLine("skipping symlink: " + fn)
            else
                filepath.Trigger("D::" + fn)
                stage.Trigger(2)
            //let dirinfo = new DirectoryInfo(fn)
            //for fp in dirinfo.EnumerateFiles() do
            //    addFile fp.FullName
        ()

    let rec addDir fn = 
        SBCLab.LXR.Logging.log () <| Printf.sprintf "addDir %A" fn
        // must be a directory
        // recursively add all files
        if SBCLab.LXR.FileCtrl.dirExists fn then
            try
                let dirinfo = new DirectoryInfo(fn) in
                if dirinfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    || dirinfo.Attributes.HasFlag(FileAttributes.System) then
                    Console.WriteLine("skipping symlink: " + fn)
                else
                    addDir1 fn
                    for fp in dirinfo.EnumerateDirectories() do
                        addDir fp.FullName
            with
            | _ -> ()
        ()

    let selectDirectoryDialog (e : EventArgs) : string option =
        let fd = new SelectFolderDialog("select directory")
        fd.CanCreateFolders <- true
        fd.Multiselect <- false
        if fd.Run() then
            let d = fd.Folder
            //fd.Dispose()
            Some d
        else
            //fd.Dispose()
            None

    let editOptions (e : EventArgs) =
        let win = new Xwt.Dialog()
        win.Title <- "edit options"
        win.Size <- new Size(400.0, 480.0)
        win.ShowInTaskbar <- false
        win.Buttons.Add(new DialogButton("OK", Command.Ok))
        win.Buttons.Add(new DialogButton("cancel", Command.Cancel))
        reached_stage.Add(fun i -> if i = 3 then win.Close() |> ignore)
        let vbox = new Xwt.VBox()
        let p1 = new Xwt.TextEntry()  // output chunk path
        let p2 = new Xwt.TextEntry()  // output meta path
        let p3 = new Xwt.SpinButton() // number of chunks
        let p4 = new Xwt.CheckBox("use compression")
        let p5 = new Xwt.RadioButtonGroup() // deduplication level
        let p6 = new Xwt.VBox()
        p6.Visible <- false

        let lblwidth = 135.0
        vbox.PackStart(
            let hbox = new Xwt.HBox()
            hbox.PackStart(
                let l = new Xwt.Label("path to output data")
                l.WidthRequest <- lblwidth
                l
                )
            hbox.PackStart(
                p1.Name <- "pathOutput"
                p1.ExpandHorizontal <- true
                p1.Text <- optOutP
                p1
                )
            hbox.PackEnd(
                FshButton.createWithHandler "select" 
                    (fun btn e -> match selectDirectoryDialog e with
                                  | Some d -> p1.Text <- d
                                  | None -> () )
                )
            hbox
            )
        vbox.PackStart(
            let hbox = new Xwt.HBox()
            hbox.PackStart(
                let l = new Xwt.Label("path to key data")
                l.WidthRequest <- lblwidth
                l)
            hbox.PackStart(
                p2.Name <- "pathData"
                p2.ExpandHorizontal <- true
                p2.Text <- optDatP
                p2
                )
            hbox.PackEnd(
                FshButton.createWithHandler "select" 
                    (fun btn e -> match selectDirectoryDialog e with
                                  | Some d -> p2.Text <- d
                                  | None -> () )
                )
            hbox
            )
        vbox.PackStart(
            let hbox = new Xwt.HBox()
            hbox.PackStart(
                let l = new Xwt.Label("number of chunks")
                l.WidthRequest <- lblwidth
                l )
            hbox.PackStart(
                p3.Digits <- 0
                p3.Name <- "numChunks"
                p3.IncrementValue <- 1.0
                p3.ExpandHorizontal <- true
                p3.MaximumValue <- 256.0
                p3.MinimumValue <- 16.0
                p3.Value <- float optN
(*                p3.LostFocus.Add(fun e -> let n = ref 0
                                          if Int32.TryParse(p3.Text, n) then
                                            if !n < 16 then p3.Text <- "16"
                                            if !n > 256 then p3.Text <- "256" ) *)
                p3 )
            hbox.PackEnd(
                let l = new Xwt.Label((optN * 256 * 1024).ToString())
                p3.ValueChanged.Add(fun e -> l.Text <- (int p3.Value * 256 * 1024).ToString() )
(*                p3.Changed.Add(fun e -> let n = ref 0
                                        if Int32.TryParse(p3.Text, n) then
                                            l.Text <- (!n * 256 * 1024).ToString() ) *)
                l )
            hbox
            )
        vbox.PackStart(
            let hbox = new Xwt.HBox()
            hbox.PackStart(p4)
            p4.TooltipText <- "file blocks are compressed if this leads to a significant reduction in size."
            p4.Active <- 1 = optComp
            hbox
            )
        vbox.PackStart(
            let hbox = new Xwt.HBox()
            p5.ClearActive()
            Console.WriteLine("optDedup = {0}", optDedup)
            hbox.PackStart(
                let l = new Xwt.Label("deduplication")
                l.WidthRequest <- lblwidth
                l )
            hbox.PackStart(
                let p = new Xwt.RadioButton("none")
                p.Tag <- 0
                p.Group <- p5
                if 0 = optDedup then
                    p.Active <- true
                p.TooltipText <- "turns off deduplication"
                p )
            hbox.PackStart(
                let p = new Xwt.RadioButton("file")
                p.Tag <- 1
                p.Group <- p5
                if 1 = optDedup then
                    p.Active <- true
                p.TooltipText <- "computes a checksum over the entire file to detect a change"
                p )
            hbox.PackStart(
                let p = new Xwt.RadioButton("block")
                p.Tag <- 2
                p.Group <- p5
                if 2 = optDedup then
                    p.Active <- true
                p.TooltipText <- "looks at every block of 64 KB in the file"
                p )
            hbox
            )

        win.Content <- vbox
        win.Show()
        if win.Run() = Command.Ok then
           // apply data
(*           let n = ref 256
           if Int32.TryParse(p3.Text, n) then
               Parameter.setParameter "nchunks" !n
               optN <- !n *)
           optN <- int p3.Value
           Parameter.setParameter "nchunks" optN
           optOutP <- p1.Text
           Parameter.setParameter "outputdir" optOutP
           optDatP <- p2.Text
           Parameter.setParameter "dbdir" optDatP
           optComp <- if p4.Active then 1 else 0
           Parameter.setParameter "compression" optComp
           if p5.ActiveRadioButton <> null then
               optDedup <- match p5.ActiveRadioButton.Tag with
                           | :? int as i -> i
                           | _ -> 0
               Parameter.setParameter "deduplication" optDedup
               Console.WriteLine("radio btn {0} = {1}", p5.ActiveRadioButton.Label, p5.ActiveRadioButton.Tag)
           //MessageDialog.ShowMessage("output: " + optOutP + " db: " + optDatP + " n=" + optN.ToString())
           ()
        win.Close() |> ignore
        ()

    let readFile (e : EventArgs) =
        let fd = new OpenFileDialog("select files to backup")
        fd.Multiselect <- true
        if fd.Run() then
            let fps = fd.FileNames
            fd.Dispose()
            Array.iter (fun fp -> addFile fp) fps
        ()

    let readDir1 (e : EventArgs) =
        let fd = new SelectFolderDialog("select directories to backup (depth 1 only)")
        fd.CanCreateFolders <- false
        fd.Multiselect <- true
        if fd.Run() then
            let fds = fd.Folders
            fd.Dispose()
            Array.iter (fun d -> addDir1 d) fds
        ()

    let readDir (e : EventArgs) =
        let fd = new SelectFolderDialog("select directory to backup (recursively)")
        fd.CanCreateFolders <- false
        fd.Multiselect <- false
        if fd.Run() then
            let d = fd.Folder
            fd.Dispose()
            addDir d
        ()

    let summarize (e : EventArgs) =
        for i in [1..dta.RowCount] do
            let fp = dta.GetValue(i-1, df1)
            Console.WriteLine("  @ {0} = {1}", i, fp)

        stage.Trigger(3)
        ()

    let start (e : EventArgs) =
        stage.Trigger(4)
        // the controller
        let ctrl = 
            let o = new SBCLab.LXR.Options()
            o.setNchunks optN
            o.setCompression (1 = optComp)
            o.setDeduplication optDedup
            o.setRedundancy optRed
            o.setFpathChunks optOutP
            o.setFpathDb optDatP
            SBCLab.LXR.BackupCtrl.create o

        // helper
        let dobackup fp =
            let fi = FileInfo(fp)
            if fi.Attributes.HasFlag(FileAttributes.ReparsePoint)
               || fi.Attributes.HasFlag(FileAttributes.System) then
                Console.WriteLine("skipping: {0}", fp)
            else
                SBCLab.LXR.BackupCtrl.backup ctrl fp
            ()
        // do heavy work
        Console.WriteLine("we have {0} filepaths to backup.", dta.RowCount)
        for i in [1..dta.RowCount] do
            let fp0 = dta.GetValue(i-1, df1)
            let fp = fp0.Substring(3)
            if fp0.StartsWith("D::") then
                let di = DirectoryInfo(fp)
                for fps in di.EnumerateFiles() do
                    //Console.WriteLine("  @ {0} = {1}", i, fps.FullName)
                    dobackup fps.FullName
            if fp0.StartsWith("F::") then
                //Console.WriteLine("  @ {0} = {1}", i, fp)
                dobackup fp

        SBCLab.LXR.BackupCtrl.finalize ctrl

        let m = String.Format("we have transferred {0} bytes in and {1} bytes out\nit took {2}ms for encryption {3}ms to read and {4}ms to write",
                        SBCLab.LXR.BackupCtrl.bytes_in ctrl,
                        SBCLab.LXR.BackupCtrl.bytes_out ctrl,
                        SBCLab.LXR.BackupCtrl.time_encrypt ctrl,
                        SBCLab.LXR.BackupCtrl.time_extract ctrl,
                        SBCLab.LXR.BackupCtrl.time_write ctrl)
        MessageDialog.ShowMessage("Backup complete", m)

        // end work and return to initial stage
        stage.Trigger(0)
        ()

    let cancel (e : EventArgs) =
        stage.Trigger(5)
        // do cleanup

        // end work and return to initial stage
        stage.Trigger(0)
        ()

    let select_disk (drv: DriveInfo) =
        ()

    let show_disks (view: Xwt.ScrollView) =
        let t = new Table()
        Application.TimeoutInvoke(500, fun _ -> 
            t.Clear()
            t.Add(new Label("name"), 0, 0)
            t.Add(new Label("path"), 1, 0)
            t.Add(new Label("format"), 2, 0)
            t.Add(new Label("size"), 3, 0)
            t.Add(new Label("free"), 4, 0)
            System.IO.DriveInfo.GetDrives() |>
                Seq.iteri (fun i (drv: DriveInfo) -> 
                               let r = FshButton.createWithHandler drv.Name
                                             (fun b e -> if SBCLab.LXR.FileCtrl.dirExists drv.RootDirectory.FullName then
                                                            optDatP <- drv.RootDirectory.FullName
                                                            //Application.Invoke(fun _ ->  stage.Trigger(4))
                                             )
                               t.Add(r, 0, i+1)
                               t.Add(new Label(drv.RootDirectory.FullName), 1, i+1)
                               t.Add(new Label(drv.DriveFormat), 2, i+1)
                               t.Add(new Label(Printf.sprintf "%i GB" (drv.TotalSize / 1024L / 1024L / 1024L)), 3, i+1)
                               t.Add(new Label(Printf.sprintf "%i %%" (drv.TotalFreeSpace * 100L / (max 1L  drv.TotalSize))), 4, i+1)
                          )
            view.Visible
            ) |> ignore
        view.Content <- t
        view.Visible <- true

    let show_network (view: Xwt.ScrollView) =
        let t = new Table()
        t.Add(new Label("name"), 0, 0)
        t.Add(new Label("status"), 1, 0)
        t.Add(new Label("speed"), 2, 0)
        Application.TimeoutInvoke(500, fun _ -> 
            t.Clear()
            t.Add(new Label("name"), 0, 0)
            t.Add(new Label("status"), 1, 0)
            t.Add(new Label("speed"), 2, 0)
            System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces() |>
                Seq.iteri (fun i iface -> t.Add(new Label(iface.Id), 0, i+1)
                                          t.Add(new Label(iface.OperationalStatus.ToString()), 1, i+1)
                                          t.Add(new Label(iface.Speed.ToString()), 2, i+1)
                                          )
            view.Visible
            ) |> ignore            
        view.Content <- t
        view.Visible <- true

    let initialize () =
        let mainwindow = new MainWindow.Window()
        let mainbox = new HBox()
        mainbox.ExpandHorizontal <- true
        mainbox.ExpandVertical <- true
        mainbox.Spacing <- 0.0
        mainbox.MarginTop <- 0.0
        mainbox.MarginRight <- 0.0
        mainbox.MarginLeft <- 0.0
        mainbox.MarginBottom <- 0.0
        mainbox.PackStart(
            let leftdiv = new Frame()
            leftdiv.HeightRequest <- 1.0
            leftdiv.MinWidth <- 30.0
            leftdiv.BackgroundColor <- Coloring.nantucket //.FromBytes(154uy,171uy,185uy)
            leftdiv )
        let maindiv = new VBox()
        maindiv.ExpandVertical <- true
        maindiv.ExpandHorizontal <- true
        maindiv.Spacing <- 0.0
        maindiv.MarginTop <- 0.0
        maindiv.MarginRight <- 0.0
        maindiv.MarginLeft <- 0.0
        maindiv.MarginBottom <- 0.0
        //maindiv.HeightRequest <- 1.0
        maindiv.MinWidth <- 560.0
        maindiv.BackgroundColor <- Coloring.nantucket  //.FromBytes(154uy,171uy,185uy)
        mainbox.PackStart( maindiv )
        mainbox.PackEnd(
            let rightdiv = new Frame()
            rightdiv.HeightRequest <- 1.0
            rightdiv.MinWidth <- 30.0
            rightdiv.BackgroundColor <- Coloring.nantucket //.FromBytes(154uy,171uy,185uy)
            rightdiv )
        let fontbold = Font.SystemFont.WithWeight(FontWeight.Bold)
        maindiv.PackStart(
            let f = new Frame()
            f.MinHeight <- 10.0
            f )
        maindiv.PackStart(
            let logodiv = new HBox()
            logodiv.BackgroundColor <- Coloring.ink //.FromBytes(25uy, 52uy, 70uy)
            logodiv.PackStart(
                let logo1 = new Label("eLyKseeR")
                logo1.TextColor <- Coloring.jasmine //.FromBytes(233uy, 199uy, 123uy)
                logo1.Font <- fontbold
                logo1 )
            logodiv.PackStart(
                let logo1 = new Label("LXRbackup")
                logo1.TextColor <- Coloring.jasmine //.FromBytes(233uy, 199uy, 123uy)
                logo1.Font <- fontbold
                logo1 )
            logodiv.PackEnd(
                let logo1 = new Label("version 1.0")
                logo1.TextColor <- Coloring.jasmine //.FromBytes(233uy, 199uy, 123uy)
                logo1 )
            logodiv )
        let topcontent = new Frame()
        let topview = new ScrollView()
        topcontent.MinHeight <- 240.0
        topcontent.Content <- topview
        maindiv.PackStart(topcontent)

        let centercontent = new Table()
        centercontent.HeightRequest <- 100.0
        centercontent.ExpandVertical <- true
        centercontent.ExpandHorizontal <- false
        maindiv.PackStart(centercontent)
        let te = new TextEntry()
        te.ExpandHorizontal <- true
        te.Font <- Font.SystemFont.WithSize(14.0)
        te.WidthRequest <- 560.0 - 35.0
        te.Visible <- false
        te.Sensitive <- false
        te.SetDragDropTarget(TransferDataType.Uri)
        te.DragDrop.Add(fun e -> Console.WriteLine("drop: {0} T={1}", e.Action, e.Data.GetValue(TransferDataType.Uri))
                                 (*(fun i (u:Uri) -> Console.WriteLine("   @ {0} = {1}", i, u.AbsolutePath)
                                                   addFile u.AbsolutePath
                                                   addDir u.AbsolutePath ) 0 e.Data.Uris.[0]*)
                       )
        reached_stage.Add(fun i -> if i = 1 || i = 2 || i = 3 then te.Visible <- true
                                   else te.Visible <- false
                                   if i = 1 then
                                      te.Sensitive <- true
                                      te.SetFocus()
                                   else te.Sensitive <- false
                                   //if i = 2 then
                                   //   Application.Invoke(fun _ -> addDir te.Text)
                         )
        let bsel = FshButton.imageWithHandler "select" "select.png" 
                    (fun btn e -> match selectDirectoryDialog e with
                                  | Some d -> Application.Invoke(fun _ -> te.Text <- d)
                                              ()
                                  | None -> () )
        bsel.ExpandVertical <- false
        bsel.ExpandHorizontal <- false
        bsel.MinHeight <- 30.0
        bsel.Visible <- false
        bsel.ImagePosition <- ContentPosition.Center
        reached_stage.Add(fun i -> if i = 1 || i = 2 || i = 3 then bsel.Visible <- true
                                   else bsel.Visible <- false
                                   if i = 1 then bsel.Sensitive <- true
                                   else bsel.Sensitive <- false
                         )

        centercontent.Add(te, 0, 0)
        centercontent.Add(bsel, 1, 0) 

        reached_stage.Add(fun i -> match i with
                                   | 0 -> ()
                                   | _ -> () )
        let textcontent = new Label()
        textcontent.BackgroundColor <- Coloring.nantucket
        textcontent.TextColor <- Coloring.ink
        textcontent.MinHeight <- 240.0
        textcontent.Font <- Font.SystemFont.WithSize(14.0)
        maindiv.PackStart(textcontent)
        reached_stage.Add(fun i -> match i with
                                   | 0 -> textcontent.Text <- "Warning: please, disconnect all network interfaces\nand unplug all USB devices."
                                   | 1 -> textcontent.Text <- "This path (folder or file) is going to be encrypted\nand copied into the archive."
                                   | 2 -> textcontent.Text <- "Please, select the USB stick to hold private data\n(i.e. meta data and encryption keys)"
                                   | _ -> textcontent.Text <- ""
                         )

        let bottombox = new HBox()
        maindiv.PackEnd(
            let f = new Frame()
            f.MinHeight <- 10.0
            f )
        maindiv.PackEnd(bottombox)
        bottombox.PackStart(
            let b = FshButton.imageWithHandler "..." "options.png"
                        (fun btn e -> Console.WriteLine("button clicked: {0}", btn.Label)
                                      editOptions e )
            reached_stage.Add(fun i -> if i = 2 then b.Visible <- true
                                       else b.Visible <- false)
            b
            )
        let btncont = FshButton.imageWithHandler "continue" "continue.png" 
                         (fun btn e -> let s = btn.Tag :?> int
                                       if s < 5 then
                                          btn.Tag <- (s + 1)
                                          stage.Trigger(s + 1)
                         )
        btncont.Tag <- 0
        reached_stage.Add(fun i -> if i = 1 then
                                      // select path
                                      btncont.Visible <- false
                                      te.Changed.Add(fun t -> if SBCLab.LXR.FileCtrl.dirExists te.Text then
                                                                 btncont.Visible <- true
                                                              else
                                                                 btncont.Visible <- false
                                                    )
                                   elif i = 2 then
                                      // select output drive
                                      btncont.Visible <- false
                                   elif i = 3 then
                                      btncont.Visible <- true
                                      //b.Label <- "start"
                                      btncont.Image <- Image.FromResource("start.png")
                                   elif i = 4 then
                                      //b.Label <- "cancel"
                                      btncont.Image <- Image.FromResource("cancel.png")
                                   elif i = 5 then
                                      //b.Label <- "close"
                                      btncont.Image <- Image.FromResource("close.png")
                         )
        bottombox.PackEnd(
            btncont
            )
        reached_stage.Add(fun i -> match i with
                                   | 0 -> show_network topview
                                   | 1 -> topview.Visible <- false
                                   | 2 -> show_disks topview
                                   | 3 -> topview.Visible <- false
                                   | _ -> () )








(*        let vpaned = new VPaned()
        vpaned.BackgroundColor <- Color.FromBytes(154uy,171uy,185uy)
        vpaned.PositionFraction <- 1.0
        let statusbar = new TextEntry()
        statusbar.ExpandHorizontal <- true
        statusbar.WidthRequest <- 1.0
        statusbar.BackgroundColor <- Colors.LightSlateGray
        show_statbar.Add(fun m -> statusbar.Text <- m)
        reached_stage.Add(fun i -> publish_statbar <|
                                     match i with
                                     | 0 -> "set backup options"
                                     | 1 -> "list files to backup (also drag'n'drop)"
                                     | 2 -> String.Format("selected {0} entries to backup", dta.RowCount)
                                     | 3 -> "start backup of files"
                                     | 4 -> "backup in process ..."
                                     | 5 -> "cancelling backup ..."
                                     | _ -> "no idea!" )
        vpaned.Panel2.Content <- statusbar
        let hpaned = new HPaned()
        hpaned.PositionFraction <- 0.9
        let log = new ListView(dta)
        log.Columns.Add("filepath" , df1) |> ignore
        enter_filepath.Add(fun fp -> let r = dta.AddRow()
                                     dta.SetValue(r, df1, fp))
        reached_stage.Add(fun i -> if i = 0 then dta.Clear())
        log.KeyReleased.Add(fun e -> Console.WriteLine("key: {0}/{1} on row: {2}", e.NativeKeyCode, e.Key.ToString(), log.SelectedRows)
                                     if e.Key.ToString() = "Delete" then
                                        Array.iteri (fun i n -> dta.RemoveRow(n-i)) log.SelectedRows
                                        stage.Trigger(2) )
        log.SelectionMode <- SelectionMode.Multiple
        log.Sensitive <- false;
        reached_stage.Add(fun i -> if i = 2 || i = 1 then log.Sensitive <- true else log.Sensitive <- false)
        log.HeightRequest <- 660.0
        log.MinWidth <- 120.0
        log.ExpandVertical <- true
        log.ExpandHorizontal <- true
        log.SetDragDropTarget(TransferDataType.Uri)
        log.DragDrop.Add(fun e -> Console.WriteLine("drop: {0} T={1}", e.Action, e.Data.GetValue(TransferDataType.Uri))
                                  Array.iteri (fun i (u:Uri) -> Console.WriteLine("   @ {0} = {1}", i, u.AbsolutePath)
                                                                addFile u.AbsolutePath
                                                                addDir u.AbsolutePath ) e.Data.Uris )
        hpaned.Panel1.Content <- log
        let btns = new VBox()
        btns.MinWidth <- 60.0
        btns.PackStart(
            let b = FshButton.createWithHandler "Options" 
                        (fun btn e -> Console.WriteLine("button clicked: {0}", btn.Label)
                                      //btn.Visible <- false
                                      editOptions e
                                      stage.Trigger(1) )
            reached_stage.Add(fun i -> if i = 0 then b.Visible <- true
                                       if i = 3 then b.Visible <- false)
            b
            )
        btns.PackStart(
            let b = FshButton.createWithHandler "+ File/1"
                        (fun btn e -> Console.WriteLine("button clicked: {0}", btn.Label)
                                      readFile e )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 1 || i = 2 then b.Visible <- true else b.Visible <- false)
            b
            )
        btns.PackStart(
            let b = FshButton.createWithHandler "+ Dir/1"
                        (fun btn e -> Console.WriteLine("button clicked: {0}", btn.Label)
                                      readDir1 e )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 1 || i = 2 then b.Visible <- true else b.Visible <- false)
            b
            )
        btns.PackStart(
            let b = FshButton.createWithHandler "+ Dir/*"
                        (fun btn e -> Console.WriteLine("button clicked: {0}", btn.Label)
                                      mainwindow.Content.Cursor <- CursorType.Wait
                                      mainwindow.Present()
                                      readDir e
                                      mainwindow.Content.Cursor <- CursorType.Arrow )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 1 || i = 2 then b.Visible <- true else b.Visible <- false)
            b
            )
        btns.PackStart(
            let b = FshButton.createWithHandler "summarize" 
                        (fun btn e -> Console.WriteLine("button clicked: {0}", btn.Label)
                                      summarize e )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 2 then b.Visible <- true)
            reached_stage.Add(fun i -> if i = 3 then b.Visible <- false)
            b
            )
        btns.PackEnd(
            let b = FshButton.createWithHandler "start" 
                        (fun btn e -> Console.WriteLine("button clicked: {0}", btn.Label)
                                      btn.Visible <- false
                                      start e )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 3 then b.Visible <- true)
            b
            )
        btns.PackEnd(
            let b = FshButton.createWithHandler "cancel" 
                        (fun btn e -> Console.WriteLine("button clicked: {0}", btn.Label)
                                      btn.Visible <- false
                                      cancel e )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 4 then b.Visible <- true else b.Visible <- false)
            b
            )
        hpaned.Panel2.Content <- btns
        vpaned.Panel1.Content <- hpaned *)

        mainwindow.Content <- mainbox //vpaned
        mainwindow.Show()
        stage.Trigger(0)
        mainwindow   // return top widget

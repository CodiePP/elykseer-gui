namespace LXRrestore

module ThisApplication = 
    open System
    open System.Threading
    open System.Threading.Tasks
    open System.IO
    open Xwt
    open Fsh

   // events which control control flow
    let stage = new Event<int>()
    let reached_stage = stage.Publish

    // event which populate list of filepaths
    let filepath = new Event<string>()
    let enter_filepath = filepath.Publish

    // displays progress
    let progress = new Event<double>()
    let progress_report = progress.Publish

    // display error
    let msgerr = new Event<string>()
    let show_msgerr = msgerr.Publish

    let outputdir = ref <| Parameter.stringOrDefault "outputdir" "/tmp/";
    let selOutputDir (e : EventArgs) =
        let fd = new SelectFolderDialog("select output directory")
        fd.CurrentFolder <- Parameter.stringOrDefault "outputdir" "/tmp/"
        fd.CanCreateFolders <- true
        fd.Multiselect <- false
        if fd.Run() then
            Parameter.setParameter "outputdir" fd.Folder
            outputdir := fd.Folder
        !outputdir <> ""

    let inputdir = ref <| Parameter.stringOrDefault "inputdir" "/tmp/LXR";
    let selInputDir (e : EventArgs) =
        let fd = new SelectFolderDialog("select input directory")
        fd.CurrentFolder <- Parameter.stringOrDefault "inputdir" "/tmp/LXR"
        fd.CanCreateFolders <- true
        fd.Multiselect <- false
        if fd.Run() then
            Parameter.setParameter "inputdir" fd.Folder
            inputdir := fd.Folder
        !inputdir <> ""


    // the data model
    let df1 = new DataField<string>()
    let dta = new ListStore(df1)

    // the controller
    let ctrl = 
        SBCLab.LXR.RestoreCtrl.create ()


    let parseFpXml fn =
        if SBCLab.LXR.FileCtrl.fileExists fn then
            use str = new IO.FileStream(fn, IO.FileMode.Open)
            let db = SBCLab.LXR.RestoreCtrl.getDbFp ctrl
            //SBCLab.LXR.DbFp.inStream db (new StreamReader(str))
            db.inStream (new StreamReader(str))
            Console.WriteLine("know of {0} filepaths in db", db.idb.count)
            dta.Clear()
            db.idb.appKeys (fun fp -> filepath.Trigger(fp))
            stage.Trigger(3)
        else
            MessageDialog.ShowError("cannot read from file " + fn)

    let readFpXml (e : EventArgs) =
        let fd = new OpenFileDialog("load filepath XML")
        fd.CurrentFolder <- Parameter.stringOrDefault "dbdir" "/tmp/"
        fd.Multiselect <- false
        if fd.Run() then
            let fname = fd.FileName
            let fp = FileInfo(fname).DirectoryName
            Parameter.setParameter "dbdir" fp
            parseFpXml fname
        ()

    let parseKeysXml fn = 
        if SBCLab.LXR.FileCtrl.fileExists fn then
            use str = new IO.FileStream(fn, IO.FileMode.Open)
            let db = SBCLab.LXR.RestoreCtrl.getDbKeys ctrl
            db.inStream (new StreamReader(str))
            Console.WriteLine("know of {0} keys in db", db.idb.count)
            stage.Trigger(3)
        else
            MessageDialog.ShowError("cannot read from file " + fn)

    let readKeysXml (e : EventArgs) =
        let fd = new OpenFileDialog("load keys XML")
        fd.CurrentFolder <- Parameter.stringOrDefault "dbdir" "/tmp/"
        fd.Multiselect <- false
        if fd.Run() then
            let fname = fd.FileName
            let fp = FileInfo(fname).DirectoryName
            Parameter.setParameter "dbdir" fp
            parseKeysXml fname
        ()

    let validate (e : EventArgs) =
        let dbfp = SBCLab.LXR.RestoreCtrl.getDbFp ctrl
        let dbkey = SBCLab.LXR.RestoreCtrl.getDbKeys ctrl
        let mutable hasall = true
        let mutable count = 0
        let mutable blen = 0
        let mutable clen = 0
        for i in [1..dta.RowCount] do
            let fp = dta.GetValue(i-1, df1)
            let dat0 = dbfp.idb.get fp
            let hasall', count', blen', clen' = 
                match dat0 with
                    | None -> (false, count, blen, clen)
                    | Some dat -> (List.forall (fun (b : SBCLab.LXR.DbFpBlock) ->
                                      let said = SBCLab.LXR.Key256.toHex b.aid
                                      Option.isSome (dbkey.idb.get said))
                                      dat.blocks, 
                                   count + 1, blen + (List.fold (fun s (e:SBCLab.LXR.DbFpBlock) -> s + e.blen) 0 dat.blocks),
                                   clen + (List.fold (fun s (e:SBCLab.LXR.DbFpBlock)-> s + e.clen) 0 dat.blocks)
                                  )
            hasall <- hasall && hasall'
            count <- count'
            blen <- blen'
            clen <- clen'

        if hasall then
            stage.Trigger(4) // succeed
            let m = Printf.sprintf "total files: %d\ntotal bytes: %.1f MB\ncompressed: %.1f MB\ncompression: %.1f %%"
                      count
                      (float blen / 1024.0 / 1024.0)
                      (float clen / 1024.0 / 1024.0)
                      (100.0 * float blen / float clen - 100.0)
            Console.WriteLine(m)
            m
        else
            "some keys may be missing;\ncannot continue."

 
    // this is being executed in its own thread
    // therefore, we have to invoke methods from the main thread
    // (i.e. Application.Invoke)
    let start (cxsrc : CancellationTokenSource) (e : EventArgs) =
        Application.Invoke(fun _ -> stage.Trigger(5))
        //Console.WriteLine("now in thread {0}", Thread.CurrentThread.Name)
        Thread.CurrentThread.Name <- "heavy work"
        // do heavy work
        //Console.WriteLine("we have {0} filepaths to restore.", dta.RowCount)
        //for i in [1..dta.RowCount] do
        //    Console.WriteLine("  @ {0} = {1}", i, dta.GetValue(i-1, df1))

        try        
            let o = new SBCLab.LXR.Options()
            o.setNchunks 256
            o.setRedundancy 0
            o.setFpathChunks !inputdir
            //o.setFpathDb ""  //dbdir
            SBCLab.LXR.RestoreCtrl.setOptions ctrl  o

            let count = dta.RowCount
            let i = ref 0
            while (!i < count && not cxsrc.IsCancellationRequested) do
                Application.Invoke(fun _ -> progress.Trigger( double !i / double count ))
                //System.Threading.Thread.Sleep(200)
                let fp = dta.GetValue(!i, df1)
                SBCLab.LXR.RestoreCtrl.restore ctrl !outputdir fp
                i := !i + 1
        with
          | SBCLab.LXR.RestoreCtrl.BadAccess -> Application.Invoke(fun _ -> msgerr.Trigger("Cannot extract files;\nmaybe already some files in the target directory?"))
          | SBCLab.LXR.RestoreCtrl.NoKey -> Application.Invoke(fun _ -> msgerr.Trigger("Some encryption key is not present;\ncannot decrypt and extract some files."))
          | SBCLab.LXR.RestoreCtrl.ReadFailed m -> Application.Invoke(fun _ -> msgerr.Trigger("Extraction failed: " + m))
          | e -> Application.Invoke(fun _ -> msgerr.Trigger(e.ToString()))

        // end work and return to initial stage
        Application.Invoke(fun _ -> stage.Trigger(0))
        ()

    let cancel (cxsrc : CancellationTokenSource) (e : EventArgs) =
        stage.Trigger(6)

        // request cancel
        cxsrc.Cancel()

        System.Threading.Thread.Sleep(2000)

        // end work and return to initial stage
        stage.Trigger(0)
        ()

    let initialize () =
        Thread.CurrentThread.Name <- "mainloop"
        let mainwindow = new MainWindow.Window()
        //show_msginfo.Add(fun m -> MessageDialog.ShowMessage(Thread.CurrentThread.Name + " " + m))
        show_msgerr.Add(fun m -> MessageDialog.ShowError(m))
        let vpaned = new VPaned()
        vpaned.PositionFraction <- 1.0
        let statusbar = new TextEntry()
        statusbar.ExpandHorizontal <- true
        statusbar.WidthRequest <- 1.0
        reached_stage.Add(fun i -> statusbar.Text <- 
                                     match i with
                                     | 0 -> "select input directory with encrypted data"
                                     | 1 -> "select output directory for restored data"
                                     | 2 -> "load XML files with keys and filepaths; only leave filepaths which should be restored"
                                     | 3 -> "validate availability of encryption keys"
                                     | 4 -> "start restore of data"
                                     | 5 -> "restoring data in process ..."
                                     | 6 -> "cancelling data restoration ..."
                                     | _ -> "no idea!" )
        let fprog10 r = match r with
                        | 9 -> "...:...|"
                        | 8 -> "...:.. |"
                        | 7 -> "...:.  |"
                        | 6 -> "...:   |"
                        | 5 -> "...:   |"
                        | 4 -> "...    |"
                        | 3 -> "..     |"
                        | 2 -> ".      |"
                        | 1 -> ".      |"
                        | 0 -> "       |"
                        | _ -> "       |"
        progress_report.Add(fun p -> let perc = (p * 100.0 + 0.5) |> floor |> int  in
                                     statusbar.Text <- (Printf.sprintf "at % 3d %%  |" perc)
                                                       + (String.replicate (perc / 10) "...:...|")
                                                       + (fprog10 (perc % 10))
                                                       + (String.replicate (9 - perc / 10) "       |")
                                     statusbar.ShowFrame <- true
                                     statusbar.Show()
                                     Console.WriteLine(statusbar.Text) )
        vpaned.Panel2.Content <- statusbar
        let hpaned = new HPaned()
        hpaned.PositionFraction <- 0.9
        let log = new ListView(dta)
        log.Columns.Add("selected filepaths" , df1) |> ignore
        enter_filepath.Add(fun fp -> let r = dta.AddRow()
                                     dta.SetValue(r, df1, fp))
        reached_stage.Add(fun i -> if i = 0 then dta.Clear())
        log.KeyReleased.Add(fun e -> Console.WriteLine("key: {0}/{1} on row: {2}", e.NativeKeyCode, e.Key.ToString(), log.SelectedRows)
                                     if e.Key.ToString() = "Delete" then Array.iteri (fun i n -> dta.RemoveRow(n-i)) log.SelectedRows )
        log.SelectionMode <- SelectionMode.Multiple
        log.Sensitive <- false;
        reached_stage.Add(fun i -> if i = 3 || i = 2 then log.Sensitive <- true else log.Sensitive <- false)
        log.HeightRequest <- 660.0
        log.MinWidth <- 120.0
        log.ExpandVertical <- true
        log.ExpandHorizontal <- true
        log.SetDragDropTarget(TransferDataType.Uri)
        log.DragDrop.Add(fun e -> Console.WriteLine("drop: {0} T={1}", e.Action, e.Data.GetValue(TransferDataType.Uri))
                                  Array.iteri (fun i (u:Uri) -> Console.WriteLine("   @ {0} = {1}", i, u.AbsolutePath)
                                                                parseFpXml u.AbsolutePath
                                                                parseKeysXml u.AbsolutePath ) e.Data.Uris )
        hpaned.Panel1.Content <- log
        let btns = new VBox()
        btns.MinWidth <- 80.0
        let mutable cxsrc = new CancellationTokenSource()
        btns.PackStart(
            let b = FshButton.createWithHandler "Input Dir" 
                        (fun btn e -> //Console.WriteLine("button clicked: {0}", btn.Label)
                                      if selInputDir e then begin
                                          stage.Trigger(1)
                                          btn.Visible <- false
                                      end )
            reached_stage.Add(fun i -> if i = 0 then b.Visible <- true)
            b
            )
        btns.PackStart(
            let b = FshButton.createWithHandler "Output Dir" 
                        (fun btn e -> //Console.WriteLine("button clicked: {0}", btn.Label)
                                      if selOutputDir e then begin
                                          stage.Trigger(2)
                                          btn.Visible <- false
                                      end )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 1 then b.Visible <- true)
            b
            )
        btns.PackStart(
            let b = FshButton.createWithHandler "+ Fp XML"
                        (fun btn e -> //Console.WriteLine("button clicked: {0}", btn.Label)
                                      readFpXml e )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 2 then b.Visible <- true)
            reached_stage.Add(fun i -> if i = 4 then b.Visible <- false)
            b
            )
        btns.PackStart(
            let b = FshButton.createWithHandler "+ Keys XML" 
                        (fun btn e -> //Console.WriteLine("button clicked: {0}", btn.Label)
                                      readKeysXml e )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 2 then b.Visible <- true)
            reached_stage.Add(fun i -> if i = 4 then b.Visible <- false)
            b
            )
        btns.PackStart(
            let b = FshButton.createWithHandler "validate" 
                        (fun btn e -> //Console.WriteLine("button clicked: {0}", btn.Label)
                                      validate e |> fun m -> MessageDialog.ShowMessage(m)
                                       )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 3 then b.Visible <- true)
            reached_stage.Add(fun i -> if i = 4 then b.Visible <- false)
            b
            )
        btns.PackEnd(
            let b = FshButton.createWithHandler "start" 
                        (fun btn e -> //Console.WriteLine("button clicked: {0}", btn.Label)
                                      btn.Visible <- false
                                      try
                                          cxsrc.Dispose()
                                          cxsrc <- new CancellationTokenSource()
                                          let token = cxsrc.Token
                                          let mutable task = new Task((fun _ -> start cxsrc e), token)
                                          task.Start()
                                      with
                                          | e -> Console.WriteLine(e.ToString())
                                      )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 4 then b.Visible <- true)
            b
            )
        btns.PackEnd(
            let b = FshButton.createWithHandler "cancel" 
                        (fun btn e -> //Console.WriteLine("button clicked: {0}", btn.Label)
                                      btn.Visible <- false
                                      cancel cxsrc e )
            b.Visible <- false
            reached_stage.Add(fun i -> if i = 5 then b.Visible <- true)
            reached_stage.Add(fun i -> if i = 0 then b.Visible <- false)
            b
            )
        hpaned.Panel2.Content <- btns
        vpaned.Panel1.Content <- hpaned
        mainwindow.Content <- vpaned
        mainwindow.Show()
        stage.Trigger(0)
        mainwindow   // return top widget

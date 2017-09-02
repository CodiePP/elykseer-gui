namespace LXRrestore
open System

module Parameter = 

    let regclass = "HKEY_CURRENT_USER"
    let company = "com.sbclab.lxr"
    let product = "LXRrestore"
    let version = "2"

    let key = regclass + "\\" + company + "\\" + product + "\\" + version

    let paramOrDefault n d = 
        Microsoft.Win32.Registry.GetValue(key, n, d)

    let setParameter n v = 
        Microsoft.Win32.Registry.SetValue(key, n, v)

    let stringOrDefault n d = 
        match paramOrDefault n d with
            | :? string as s -> s
            | _ -> ""

    let intOrDefault n d = 
        match paramOrDefault n d with
            | :? int as i -> i
            | _ -> 0

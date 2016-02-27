using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using Newtonsoft.Json.Linq;

/// <summary>
/// Summary description for JsonTest
/// </summary>
public class JsonTest
{
    public static string Test()
    {
        dynamic d = JObject.Parse("{hello: ['world', 'blah']}");
        return d.hello[1];
    }
}

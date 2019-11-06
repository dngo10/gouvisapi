using GDetailsApi.Gouvis.Models;
using GDetailsApi.GradingSystem;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class GouvisDetailsController : ControllerBase
{
    public readonly GouvisContext _context;
    public SqliteConnection sc;

    public GouvisDetailsController(GouvisContext context){
        _context = context;
        sc = new SqliteConnection("Data Source=GouvisDetails.db;"); 
    }


    [HttpGet]
    public async Task<ActionResult<IEnumerable>> GetGouvisDetails(){

        await sc.OpenAsync();
        List<returnInitClass> result1 = new List<returnInitClass>();

        string sqlCmmd  = 
        @"SELECT fileName, cadName, TRIM(name) as Name1, scale, date FROM GouvisDetailsDBSet WHERE 
        fileName IS NOT NULL AND 
        cadName IS NOT NULL AND 
        Name1 IS NOT NULL AND 
        scale IS NOT NULL;";

        sqlCmmd = sqlCmmd.Replace("\r\n", " ");

        SqliteCommand sqliteCommand = sc.CreateCommand();
        sqliteCommand.CommandText = sqlCmmd;
        await sqliteCommand.ExecuteNonQueryAsync();
        SqliteDataReader r = sqliteCommand.ExecuteReader();
        while(await r.ReadAsync()){
            returnInitClass rI = new returnInitClass(   Convert.ToString(r["fileName"]), 
                                                        Convert.ToString(r["cadName"]),
                                                        Convert.ToString(r["Name1"]),
                                                        Convert.ToString(r["scale"]),
                                                        Convert.ToInt64(r["date"]));
            result1.Add(rI);
        }

        await sc.CloseAsync();
        return result1;
    }


    [HttpGet("{queryString}")]
    public async Task<IEnumerable> GetGouvisDetails(string queryString){
        queryString = queryString.Trim().ToLower();

        if(queryString.Length <= 1){
            return new List<returnInitClass>();
        }

        GradingQuery gq = new GradingQuery();
        
        await gq.Grading(queryString);
        return gq.result.Where(item => item.point != 0).OrderByDescending(item => item.point).ThenByDescending(item => item.name).ThenByDescending(item => item.date);
    }

    [HttpPost]
    public void PostNewTag(postItem item){
        if(String.IsNullOrEmpty(item.tag) || item.tag.Length <= 3) return;
        GradingQuery gq = new GradingQuery();
        gq.updateTagGrading(item);
    }

}

public class returnInitClass{
    public string fileName{get; private set;}
    public string cadName{get;private set;}
    public string  name{get;private set;}
    public string scale{get; private set;}

    public int point{get; set;}

    public long date{get;set;}

    public returnInitClass(string fileName, string cadName, string name, string scale, long date)
    {
        this.fileName = fileName;
        this.cadName = cadName;
        this.name = name;
        this.scale = scale;
        this.date = date;
    }
}

public class postItem{
    public string fileName{get; set;}
    public string tag{get; set;}
}



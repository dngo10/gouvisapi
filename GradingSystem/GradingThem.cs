using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using F23.StringSimilarity;
using System.IO;
using System.Threading;

namespace GDetailsApi.GradingSystem
{
    public class GradingQuery{

        enum GradingPoint{
            Name = 25,
            Desc = 11,
            RawTextName = 80,
            RawTextDesc = 35,

            RawFileName = 165,
            FileName = 60,

            Tag = 200
        }

        public char[] noUseChar = new char[]{'.', '~', '`', 
        '"', '\'', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')',
        '{', '}', '|', ':', ';', '\\', '<', '>', '/', ',', '-', ' ', 
        '+', '=', '_', '[', ']'}; 

        private string conditionquery = "scale is not null and name is not null and description is not null";

        private string schemaName = "GouvisDetailsDBSet";

        public string databaseLocation = @"GouvisDetails.db";
        public string databaseLocationForTag = @"GovisDatabaseTag.db";
        public string databaseConnection ; 
        public string databaseConnectionTag ; 

        SqliteConnection sqlite_conn;
        SqliteConnection sqlite_tagList;
        public List<returnInitClass> result;
        public List<string> resultTemp;

        public GradingQuery()
        {
            databaseConnection = "Data Source=" + databaseLocation + ";";
            databaseConnectionTag = "Data Source=" + databaseLocationForTag + ";";
            result = new List<returnInitClass>();
            resultTemp = new List<string>();
            sqlite_conn = new SqliteConnection(databaseConnection);
            sqlite_tagList = new SqliteConnection(databaseConnectionTag);
        }

        public void initTheGradingList(){
            SqliteCommand sqliteCommand = sqlite_conn.CreateCommand();
            //await sqlite_conn.OpenAsync();
            sqliteCommand.CommandText = String.Format("select fileName,cadName, name, scale, date from {0} where {1};", schemaName, conditionquery);
            SqliteDataReader sreader = sqliteCommand.ExecuteReader();
            while(sreader.Read()){
                returnInitClass rIC = new returnInitClass(Convert.ToString(sreader["fileName"]), Convert.ToString(sreader["cadName"]), 
                Convert.ToString(sreader["name"]).Trim(), Convert.ToString(sreader["scale"]), Convert.ToInt64(sreader["date"]));
                result.Add(rIC);
            }
            //await sqlite_conn.CloseAsync();
        }


        /*-----------------------------------------------------------*/
        /*MAIN METHOD*/
        public async Task Grading(string queryString){
            /*This queryString is trimmed already*/
            bool stop = false;
            queryString = queryString.Trim().ToLower();

            if(indexTageFound(queryString)){
                return;
            }
            
            await sqlite_conn.OpenAsync();

            initTheGradingList();

            List<string> words = wordList(queryString);

            Task temp = Task.Run(()=>
                {
                    Thread.Sleep(10000);
                    stop = true;
                }
            );

            
            foreach(returnInitClass rIC in result){
                Task<bool> t1 = isPartOfProperty("fileName", queryString, rIC.fileName);
                Task<bool> t2 = isPartOfProperty("name", queryString, rIC.fileName);
                Task<bool> t3 = isPartOfProperty("description", queryString, rIC.fileName);

                Task<int> tTag = Task.Run(() => {
                    int TagGrade = isInTagGrading(queryString, rIC.fileName);
                    return TagGrade;
                });
                


                
                Task t4 = Task.Run(()=> {
                    foreach(string str in words){
                        if(str.Length <= 2) continue;
                        //if(str== "wall") {
                        //    continue;
                        //}

                        Task<bool> ti1 = isPartOfProperty("fileName", str, rIC.fileName);
                        Task<bool> ti2 = isPartOfProperty("description", str, rIC.fileName);
                        Task<bool> ti3 = isPartOfProperty("name", str, rIC.fileName);

                        Task.WaitAll(ti1, ti2, ti3);

                        if(ti1.Result){
                             rIC.point += (int)GradingPoint.Name;
                        }

                        if(ti2.Result){
                            rIC.point += (int)GradingPoint.Desc;
                        }

                        if(ti3.Result){
                            rIC.point += (int)GradingPoint.FileName;
                        }
                    }
                });


                Task.WaitAll(t1, t2, t3, tTag);

                if(t1.Result){
                     rIC.point += (int)GradingPoint.RawFileName;
                }

                if(t2.Result){
                    rIC.point += (int)GradingPoint.RawTextName;
                }

                if(t3.Result){
                    rIC.point += (int)GradingPoint.RawTextDesc;
                }

                if(tTag.Result > 0){
                    rIC.point += tTag.Result;
                }

                await t4;

                if(stop){
                    return;
                }
            }
            await sqlite_conn.CloseAsync();
            
        }

        public bool indexTageFound(string queryString){
            sqlite_tagList.Open();
            SqliteCommand sCommand = sqlite_tagList.CreateCommand();
            sCommand.CommandText = string.Format("SELECT * from TagIndex where tag = '{0}';", queryString);
            sCommand.ExecuteNonQuery();
            SqliteDataReader reader = sCommand.ExecuteReader();
            int i = 0;
            string query;
            while(reader.Read()){
                query = Convert.ToString(reader["jsonData"]);
                result = Newtonsoft.Json.JsonConvert.DeserializeObject<List<returnInitClass>>(query);
                i++;
            }
            sqlite_tagList.Close();

            return i==1;
        }

        public List<string> wordList(string queryString){

            if(isTextFileName(queryString)){
                    return resultTemp;
            }

            string[] primaryString = queryString.Split(noUseChar);
            List<string> primaryList = new List<string>(primaryString);
            primaryList = primaryList.Where(s => !string.IsNullOrWhiteSpace(s) && !string.IsNullOrEmpty(s)).Distinct().ToList();


            for(int i = 0; i < primaryList.Count(); i++){
                if(!isInDictionary(primaryList[i])){
                    string nearestmatch = nearestMatchingWord(primaryList[i]);
                    if(!string.IsNullOrWhiteSpace(nearestmatch)){
                        primaryList[i] = nearestmatch;
                    }
                }
            }
            return primaryList;
        }

        private bool isInDictionary(string word){

            SqliteCommand sCommand = sqlite_conn.CreateCommand();
            sCommand.CommandText = string.Format("SELECT count(*) as total FROM Word where word = '{0}';", word);
            sCommand.ExecuteNonQuery();
            SqliteDataReader reader = sCommand.ExecuteReader();
            int total = 0;
            while(reader.Read()){
                total = Convert.ToInt32(reader["total"]);
            }
            return total > 0;
        }

        private string nearestMatchingWord(string word){
            if(word.Any(c => char.IsDigit(c))){
                return word;
            }
            //Fastenshtein.Levenshtein lev = new Fastenshtein.Levenshtein(word);
            JaroWinkler jw = new JaroWinkler();
            string nearestMatch = "";

            SqliteCommand sCommand = sqlite_conn.CreateCommand();

            sCommand.CommandText = string.Format("SELECT word FROM Word;");
            sCommand.ExecuteNonQuery();
            SqliteDataReader reader = sCommand.ExecuteReader();


            //int pastDistance = 9999999;
            //int currentDistance = 9999999;
            double pastDistance = -999999;
            double currentDistance = -999999;
            string nearestTempWord = "";

            while(reader.Read()){

                nearestTempWord = Convert.ToString(reader["word"]);
                //currentDistance = lev.DistanceFrom(nearestTempWord);
                currentDistance = jw.Similarity(nearestTempWord, word);
                if(currentDistance > pastDistance){
                    pastDistance = currentDistance;
                    nearestMatch = nearestTempWord;
                }

            }
            File.AppendAllText("abc.txt", nearestMatch + "\n");
            return nearestMatch;
        }

        private bool isTextFileName(string word){

            SqliteCommand sCommand = sqlite_conn.CreateCommand();
            sCommand.CommandText = string.Format("SELECT fileName FROM {2} where cadName LIKE '%{0}%' AND {1};", word, conditionquery, schemaName);
            sCommand.ExecuteNonQuery();
            SqliteDataReader reader = sCommand.ExecuteReader();

            while(reader.Read()){
                resultTemp.Add(Convert.ToString(reader["fileName"]));
            }
            return resultTemp.Count > 0;
        }

        private Task<bool> isPartOfProperty(string attribute, string str, string fileName){
            int result = 0;
            return Task.Run(() =>{
                SqliteCommand sCommand = sqlite_conn.CreateCommand();
                sCommand.CommandText = string.Format("SELECT count(*) as Total FROM {0} WHERE fileName = '{1}' AND {2} LIKE '%{3}%' and {4};", 
                schemaName, fileName, attribute, str, conditionquery);
                SqliteDataReader reader = sCommand.ExecuteReader();

                while(reader.Read()){
                    result = Convert.ToInt32(reader["Total"]);
                }

                return result == 1;
                }
            );
            
        }

        private int isInTagGrading(string tag, string fileName){
            SqliteCommand sCommand = sqlite_conn.CreateCommand();
            sCommand.CommandText = string.Format("SELECT point FROM {0} WHERE fileName = '{1}' AND  tag = '{2}';", 
            "TagGrading", fileName, tag);
            SqliteDataReader reader = sCommand.ExecuteReader();
            int result = 0;
            while(reader.Read()){
                result = Convert.ToInt32(reader["point"]);
            }
            return result;
        }

        public async void updateTagGrading(postItem item){
            item.tag = item.tag.Trim().ToLower();
            sqlite_conn.Open();
            SqliteCommand sCommand = sqlite_conn.CreateCommand();
            sCommand.CommandText = string.Format("INSERT OR REPLACE INTO TagGrading(fileName, tag, point) values('{0}', '{1}', '{2}');", item.fileName, item.tag, (int)GradingPoint.Tag);
            sCommand.ExecuteNonQuery();
            sCommand.CommandText = string.Format("UPDATE TagGrading SET point = CASE WHEN point > 0 THEN point - 1 END WHERE fileName = '{0}' and tag = '{1}';", item.fileName, item.tag);
            sCommand.ExecuteNonQuery();
            sqlite_conn.Close();    

            result = new List<returnInitClass>();
            await Grading(item.tag);
            var sortedResult =  result.Where(item => item.point != 0).OrderByDescending(item => item.point).ThenByDescending(item => item.name).ThenByDescending(item => item.date);
            string json = System.Text.Json.JsonSerializer.Serialize(sortedResult);
            sqlite_tagList.Open();
            SqliteCommand sCommand1 = sqlite_tagList.CreateCommand();
            sCommand1.CommandText = string.Format("INSERT OR REPLACE INTO TagIndex(tag, jsonData) values('{0}', '{1}');", item.tag, json);
            sCommand1.ExecuteNonQuery();
            sqlite_tagList.Close();
        }

    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using Microsoft.PowerBI.Api.V2.Models;
using Newtonsoft.Json;

namespace gbrueckl.PowerBI.API.PowerBIObjects
{
    public class PBIDataset : Dataset, IPBIObject
    {
        #region Constructors
        [JsonConstructor]
        public PBIDataset(string name)
        {
            Name = name;

            Relationships = new List<PBIRelationship>();
        }
        #endregion
        #region Private Properties for Serialization
        [JsonProperty(PropertyName = "@odata.context", NullValueHandling = NullValueHandling.Ignore, Required = Required.Default)]
        private string ODataContext;

        [JsonProperty(PropertyName = "tables", NullValueHandling = NullValueHandling.Ignore)]
        private List<PBITable> _tables;

        [JsonIgnore]
        private List<PBIRefresh> _refreshes;

        [JsonProperty(PropertyName = "relationships", NullValueHandling = NullValueHandling.Ignore)]
        public List<PBIRelationship> Relationships { get; set; }

        [JsonProperty(PropertyName = "datasources", NullValueHandling = NullValueHandling.Ignore)]
        public new List<PBIDatasource> Datasources { get { return null; } set { } }

        [JsonIgnore]
        public List<PBIGatewayDatasource> GatewayDatasources { get; set; }

        [JsonProperty(PropertyName = "webUrl", NullValueHandling = NullValueHandling.Ignore)]
        public new string WebUrl { get { return null; } set { } }

        #endregion

        #region Public Properties
        [JsonIgnore]
        public PBIDefaultMode PBIDefaultMode
        {
            get
            {
                if (string.IsNullOrEmpty(base.DefaultMode))
                    return PBIDefaultMode.Push;

                return (PBIDefaultMode)Enum.Parse(typeof(PBIDefaultMode), base.DefaultMode, true);
            }
            set
            {
                base.DefaultMode = value.ToString();
            }
        }
        [JsonIgnore]
        public PBIDefaultRetentionPolicy PBIDefaultRetentionPolicy
        {
            get
            {
                if (string.IsNullOrEmpty(base.DefaultRetentionPolicy))
                    return PBIDefaultRetentionPolicy.None;

                return (PBIDefaultRetentionPolicy)Enum.Parse(typeof(PBIDefaultRetentionPolicy), base.DefaultRetentionPolicy, true);
            }
            set
            {
                base.DefaultRetentionPolicy = value.ToString();
            }
        }
        [JsonIgnore]
        public new IList<PBITable> Tables
        {
            get
            {
                if (_tables == null)
                {
                    if (Id != null)
                    {
                        LoadTablesFromPowerBI();
                    }
                    else
                    {
                        _tables = new List<PBITable>();
                    }
                }
                
                return _tables;
            }
            set
            {
                _tables = value.ToList();

                foreach (PBITable tbl in _tables)
                {
                    tbl.ParentGroup = this.ParentGroup;
                    tbl.ParentDataset = this;
                    tbl.ParentObject = this;
                }
            }
        }

        [JsonIgnore]
        public IList<PBIRefresh> Refreshes
        {
            get
            {
                if (_refreshes == null)
                {
                    if (Id != null)
                    {
                        LoadRefreshesFromPowerBI();
                    }
                    else
                    {
                        _refreshes = new List<PBIRefresh>();
                    }
                }

                return _refreshes;
            }
        }

        [JsonIgnore]
        public PBIAPIClient ParentPowerBIAPI { get; set; }
        [JsonIgnore]
        public PBIGroup ParentGroup { get; set; }
        [JsonIgnore]
        public string ApiURL
        {
            get
            {
                if (ParentGroup == null)
                    return string.Format("/v1.0/myorg/datasets/{0}", Id);
                else
                    return string.Format("/v1.0/myorg/groups/{0}/datasets/{1}", ParentGroup.Id, Id);
            }
        }
        [JsonIgnore]
        public IPBIObject ParentObject { get; set; }

        #endregion

        #region Public Functions
        public PBITable GetTableByName(string name)
        {
            foreach (PBITable tbl in Tables)
            {
                if (tbl.Name == name)
                    return tbl;
            }

            return null;
        }

        public void LoadTablesFromPowerBI()
        {
            PBIObjectList<PBITable> objList = JsonConvert.DeserializeObject<PBIObjectList<PBITable>>(ParentPowerBIAPI.SendGETRequest(ApiURL, PBIAPI.Tables).ResponseToString());

            foreach (var item in objList.Items)
            {
                item.ParentGroup = this.ParentGroup;
                item.ParentObject = this;
                item.ParentDataset = this;
            }

            _tables = objList.Items;
        }

        public void AddOrUpdateTable(PBITable newTable)
        {
            newTable.ParentDataset = this;
            newTable.ParentObject = this;
            newTable.ParentGroup = this.ParentGroup;

            for (int i = 0; i < Tables.Count; i++)
            {
                if (Tables[i].Name == newTable.Name)
                {
                    if(newTable.Columns != null)
                        Tables[i].Columns = Tables[i].Columns.Union(newTable.Columns).ToList();

                    if(newTable.Measures != null)
                        Tables[i].Measures = Tables[i].Measures.Union(newTable.Measures).ToList();

                    if (newTable.IsHidden.HasValue)
                        Tables[i].IsHidden = newTable.IsHidden;

                    return;
                }
            }

            Tables.Add(newTable);
        }

        public void LoadDatasourcesFromPowerBI()
        {
            PBIObjectList<PBIDatasource> objList = JsonConvert.DeserializeObject<PBIObjectList<PBIDatasource>>(ParentPowerBIAPI.SendGETRequest(ApiURL, PBIAPI.Datasources).ResponseToString());

            foreach (var item in objList.Items)
            {
                item.ParentGroup = this.ParentGroup;
                item.ParentObject = this;
            }

            Datasources = objList.Items;
        }

        public void LoadGatewayDatasourcesFromPowerBI()
        {
            PBIObjectList<PBIGatewayDatasource> objList = JsonConvert.DeserializeObject<PBIObjectList<PBIGatewayDatasource>>(ParentPowerBIAPI.SendGETRequest(ApiURL + "/Default.GetBoundGatewayDataSources").ResponseToString());

            foreach (var item in objList.Items)
            {
                item.ParentGroup = this.ParentGroup;
                item.ParentObject = this;
            }

            GatewayDatasources = objList.Items;
        }

        public void DeleteFromPowerBI(PBIAPIClient powerBiAPI)
        {
            using (HttpWebResponse response = powerBiAPI.SendDELETERequest(ApiURL))
            {
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream(), true))
                {
                    string json = streamReader.ReadToEnd();
                }
            }
        }

        public string PublishToPowerBI(PBIAPIClient powerBiAPI = null)
        {
            return PublishToPowerBI(powerBiAPI, PBIDefaultRetentionPolicy);
        }
        public string PublishToPowerBI(PBIAPIClient powerBiAPI, PBIDefaultRetentionPolicy defaultRetentionPolicy)
        {
            if (powerBiAPI == null)
            {
                if(ParentPowerBIAPI == null)
                    throw new Exception("No PowerBI API Object was supplied!");
                else
                    powerBiAPI = ParentPowerBIAPI;
            }
            if (string.IsNullOrEmpty(Id)) // Dataset was not loaded from PowerBI Service
            {
                using (HttpWebResponse response = powerBiAPI.SendPOSTRequest(ApiURL + "?defaultRetentionPolicy=" + defaultRetentionPolicy.ToString(), JsonConvert.SerializeObject(this)))
                {
                    using (StreamReader streamReader = new StreamReader(response.GetResponseStream(), true))
                    {
                        string json = streamReader.ReadToEnd();

                        JObject jObj = JObject.Parse(json);


                        Id = jObj.GetValue("id", StringComparison.InvariantCultureIgnoreCase).ToString();
                        ODataContext = jObj.GetValue("@odata.context", StringComparison.InvariantCultureIgnoreCase).ToString();

                        if(jObj.Property("defaultRetentionPolicy") != null)
                            PBIDefaultRetentionPolicy = (PBIDefaultRetentionPolicy)Enum.Parse(typeof(PBIDefaultRetentionPolicy), jObj.GetValue("defaultRetentionPolicy", StringComparison.InvariantCultureIgnoreCase).ToString(), true);

                        if (jObj.Property("addRowsAPIEnabled") != null)
                            AddRowsAPIEnabled = bool.Parse(jObj.GetValue("addRowsAPIEnabled", StringComparison.InvariantCultureIgnoreCase).ToString());
                    }
                }
            }
            else
            {
                if (PBIDefaultMode != PBIDefaultMode.Streaming)// Update is not supported for Streaming-Datasets
                {
                    foreach (PBITable table in Tables)
                    {
                        table.PublishToPowerBI(powerBiAPI);
                    }
                }
                // Other DefaultModes do not support updating a Table-Definition?!?
            }

            return Id;
        }

        public void SyncFromPowerBI(PBIAPIClient powerBiAPI = null)
        {
            if (powerBiAPI == null)
            {
                if (ParentPowerBIAPI == null)
                    throw new Exception("No PowerBI API Object was supplied!");
                else
                    powerBiAPI = ParentPowerBIAPI;
            }

            PBIDataset temp;
            try
            {
                if (string.IsNullOrEmpty(this.Id))
                    if (ParentGroup == null)
                        temp = powerBiAPI.GetDatasetByName(this.Name);
                    else
                        temp = ParentGroup.GetDatasetByName(this.Name);
                else
                    if (ParentGroup == null)
                    temp = powerBiAPI.GetDatasetByID(this.Id);
                else
                    temp = ParentGroup.GetDatasetByID(this.Id);

                if (temp != null)
                {
                    this.Id = temp.Id;
                    this.AddRowsAPIEnabled = temp.AddRowsAPIEnabled;

                    foreach (PBITable table in temp.Tables)
                    {
                        this.AddOrUpdateTable(table);
                    }
                }
            }
            catch (Exception e)
            {

            }
        }

        public void LoadRefreshesFromPowerBI()
        {
            PBIObjectList<PBIRefresh> objList = JsonConvert.DeserializeObject<PBIObjectList<PBIRefresh>>(ParentPowerBIAPI.SendGETRequest(ApiURL, PBIAPI.Refreshes).ResponseToString());
            this._refreshes = objList.Items;
        }

        public void Refresh(PBIAPIClient powerBiAPI = null)
        {
            if (powerBiAPI == null)
            {
                if (ParentPowerBIAPI == null)
                    throw new Exception("No PowerBI API Object was supplied!");
                else
                    powerBiAPI = ParentPowerBIAPI;
            }
            using (HttpWebResponse response = powerBiAPI.SendPOSTRequest(ApiURL, PBIAPI.Refreshes, null))
            {
                string result = response.ResponseToString();
            }
        }

        public void TakeOver(PBIAPIClient powerBiAPI = null)
        {
            if (powerBiAPI == null)
            {
                if (ParentPowerBIAPI == null)
                    throw new Exception("No PowerBI API Object was supplied!");
                else
                    powerBiAPI = ParentPowerBIAPI;
            }
            using (HttpWebResponse response = powerBiAPI.SendPOSTRequest(ApiURL + "/takeover", null))
            {
                string result = response.ResponseToString();
            }
        }

        public void DeleteRowsFromPowerBI(PBIAPIClient powerBiAPI)
        {
            foreach (PBITable table in Tables)
            {
                table.DeleteRowsFromPowerBI(powerBiAPI);
            }
        }
        #endregion 
    }
}

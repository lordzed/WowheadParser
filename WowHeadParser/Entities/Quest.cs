﻿/*
 * * Created by Traesh for AshamaneProject (https://github.com/AshamaneProject)
 */
using Newtonsoft.Json;
using Sql;
using System;
using System.Collections.Generic;
using System.Xml;

namespace WowHeadParser.Entities
{
    class Quest : Entity
    {
        struct QuestTemplateParsing
        {
            public int id;
            public int category;
            public int category2;
            public int[] currencyrewards;
            public int money;
            public string name;
            public int reqlevel;
            public int side;
            public int type;
        }

        public Quest() : base()
        {
            m_builderStarter = new SqlBuilder("creature_queststarter", "id");
            m_builderStarter.SetFieldsNames("quest");

            m_builderEnder = new SqlBuilder("creature_questender", "id");
            m_builderEnder.SetFieldsNames("quest");

            m_builderSerieWithPrevious = new SqlBuilder("quest_template_addon", "id", SqlQueryType.Update);
            m_builderSerieWithPrevious.SetFieldsNames("PrevQuestID", "ExclusiveGroup");

            m_builderSerieWithoutPrevious = new SqlBuilder("quest_template_addon", "id", SqlQueryType.Update);
            m_builderSerieWithoutPrevious.SetFieldsNames("ExclusiveGroup");

            m_builderRequiredTeam = new SqlBuilder("quest_template", "id", SqlQueryType.Update);

            m_builderRequiredClass = new SqlBuilder("quest_template_addon", "id", SqlQueryType.Update);
            m_builderRequiredClass.SetFieldsNames("AllowableClasses");
        }

        public Quest(int id) : this()
        {
            m_data.id = id;
        }

        public override String GetWowheadUrl()
        {
            return GetWowheadBaseUrl() + "/quest=" + m_data.id;
        }

        public override List<Entity> GetIdsFromZone(String zoneId, String zoneHtml)
        {
            String pattern = @"new Listview\(\{template: 'quest', id: 'quests', name: WH.TERMS.quests, tabs: tabsRelated, parent: 'lkljbjkb574',(.*)data: (.+)\}\);";
            String creatureJSon = Tools.ExtractJsonFromWithPattern(zoneHtml, pattern, 1);

            List<Entity> tempArray = new List<Entity>();
            if (creatureJSon != null)
            {
                List<CreatureTemplateParsing> parsingArray = JsonConvert.DeserializeObject<List<CreatureTemplateParsing>>(creatureJSon);
                foreach (CreatureTemplateParsing creatureTemplateStruct in parsingArray)
                {
                    Quest questTemplate = new Quest(creatureTemplateStruct.id);
                    tempArray.Add(questTemplate);
                }
            }

            return tempArray;
        }

        public override bool ParseSingleJson(int id = 0)
        {
            if (m_data.id == 0 && id == 0)
                return false;
            else if (m_data.id == 0 && id != 0)
                m_data.id = id;

            bool optionSelected = false;
            String questHtml = Tools.GetHtmlFromWowhead(GetWowheadUrl());

            if (questHtml.Contains("inputbox-error") || questHtml.Contains("database-detail-page-not-found-message"))
                return false;

            if (IsCheckboxChecked("starter/ender"))
            {
                String dataPattern = @"var myMapper = new Mapper\((.+)\)";

                String questDataJSon = Tools.ExtractJsonFromWithPattern(questHtml, dataPattern);
                if (questDataJSon != null)
                {
                    dynamic data = JsonConvert.DeserializeObject<dynamic>(questDataJSon);
                    SetData(data);
                    optionSelected = true;
                }
            }

            if (IsCheckboxChecked("serie"))
            {
                String seriePattern = "(<table class=\"series\">.+?</table>)";

                String questSerieXml = Tools.ExtractJsonFromWithPattern(questHtml, seriePattern);
                if (questSerieXml != null)
                {
                    SetSerie(questSerieXml);
                    optionSelected = true;
                }
            }

            if (IsCheckboxChecked("class"))
            {
                String classLinePattern = @"\[li\](?:Class|Classes): (.+)\[\\/li\]\[li\]\[icon name=quest_start\]";

                String questClassLineJSon = Tools.ExtractJsonFromWithPattern(questHtml, classLinePattern);
                if (questClassLineJSon != null)
                {
                    List<String> questClass = Tools.ExtractListJsonFromWithPattern(questClassLineJSon, @"\[class=(\d+)\]");
                    SetClassRequired(questClass);
                }
                else
                    m_builderRequiredClass.AppendFieldsValue(m_data.id, 0);
                optionSelected = true;
            }

            if (IsCheckboxChecked("team"))
            {
                bool isAlliance = questHtml.Contains(@"Side: [span class=icon-alliance]Alliance[\/span]");
                bool isHorde = questHtml.Contains(@"Side: [span class=icon-horde]Horde[\/span]");

                SetTeam(isAlliance, isHorde);
                optionSelected = true;
            }

            if (optionSelected)
                return true;
            else
                return false;
        }

        public void SetData(dynamic questData)
        {
            // Save starter and ender IDs to avoid duplicates that Wowhead sometimes has
            List<int> starterIDs = new List<int>();
            List<int> enderIDs = new List<int>();

            foreach (dynamic objective in questData.objectives)
            {
                foreach (dynamic zoneid in objective)
                {
                    foreach (dynamic levels in zoneid.levels)
                    {
                        dynamic objectiveData = levels.First;
                        if (objectiveData is Newtonsoft.Json.Linq.JArray)
                        {
                            for (int i = 0; i < objectiveData.Count; i++)
                            {
                                int npcID = objectiveData[i]["id"].ToObject<int>();

                                if (objectiveData[i]["point"] == "start" && !starterIDs.Contains(npcID))
                                {
                                    m_builderStarter.AppendFieldsValue(objectiveData[i]["id"], m_data.id);
                                    starterIDs.Add(npcID);
                                }

                                if (objectiveData[i]["point"] == "end" && !enderIDs.Contains(npcID))
                                {
                                    m_builderEnder.AppendFieldsValue(objectiveData[i]["id"], m_data.id);
                                    enderIDs.Add(npcID);
                                }
                            }

                        } else
                        {
                            if (objectiveData["point"] == "start")
                                m_builderStarter.AppendFieldsValue(objectiveData["id"], m_data.id);

                            if (objectiveData["point"] == "end")
                                m_builderEnder.AppendFieldsValue(objectiveData["id"], m_data.id);
                        }
                    }
                }
            }
        }

        public void SetSerie(String serieXml)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(serieXml);

                XmlNodeList trs = doc.DocumentElement.SelectNodes("tr");
                Dictionary<int, List<String>> questInSerieByStep = new Dictionary<int, List<String>>();
                int step = 0;

                foreach (XmlNode tr in trs)
                {
                    int currentStep = step++;
                    questInSerieByStep.Add(currentStep, new List<string>());

                    XmlNode td = tr.SelectSingleNode("td");

                    if (td == null)
                        continue;

                    XmlNode div = td.SelectSingleNode("div");

                    if (div == null)
                        continue;
                    
                    XmlNode b           = div.SelectSingleNode("b");
                    XmlNodeList aList   = div.SelectNodes("a");

                    // Current quest is writed with a simple "b" html tag
                    if (div.SelectSingleNode("b") != null)
                        questInSerieByStep[currentStep].Add(m_data.id.ToString());

                    foreach (XmlNode a in aList)
                    {
                        XmlNode hrefAttr = a.Attributes.GetNamedItem("href");

                        if (hrefAttr == null)
                            continue;

                        String href = hrefAttr.Value;
                        String questId = href.Substring(7);
                        questId = questId.Substring(0, questId.LastIndexOf("/"));

                        questInSerieByStep[currentStep].Add(questId);
                    }
                }

                if (questInSerieByStep.Count < 2)
                    return;

                for (int i = questInSerieByStep.Count - 1; i >= 0; --i)
                {
                    String previousQuest = i > 0 ? questInSerieByStep[i - 1][0]: "";

                    String exclusiveGroup = "0";
                    if (questInSerieByStep[i].Count > 1)
                        exclusiveGroup = "-" + questInSerieByStep[i][0];

                    foreach (String questId in questInSerieByStep[i])
                    {
                        if (previousQuest != "")
                            m_builderSerieWithPrevious.AppendFieldsValue(questId, previousQuest, exclusiveGroup);
                        else
                            m_builderSerieWithoutPrevious.AppendFieldsValue(questId, exclusiveGroup);
                    }
                }
            }
            catch (Exception ex)
            { }
        }

        public void SetTeam(bool isAlliance, bool isHorde)
        {
            switch (GetVersion())
            {
                case "9.2.0.42560":
                {
                    ulong team = isAlliance ? 6130900294268439629 : isHorde ? 18446744073709551615 : 0;

                    m_builderRequiredTeam.SetFieldsNames("AllowableRaces");
                    m_builderRequiredTeam.AppendFieldsValue(m_data.id, team);
                }
                break;
                default: // 8.x and 7.x
                {
                    Int32 team = isAlliance ? 0 : isHorde ? 1 : -1;

                    m_builderRequiredTeam.SetFieldsNames("requiredTeam");
                    m_builderRequiredTeam.AppendFieldsValue(m_data.id, team);
                }
                break;
            }
        }

        public void SetClassRequired(List<String> classIds)
        {
            UInt32 classMask = 0;
            foreach (String classId in classIds)
            {
                classMask += Tools.GetClassMaskFromClassId(classId);
            }

            if (classMask != 0)
                m_builderRequiredClass.AppendFieldsValue(m_data.id, classMask);
        }

        public override String GetSQLRequest()
        {
            String sqlRequest = "";

            if (IsCheckboxChecked("starter/ender"))
            {
                sqlRequest += m_builderStarter.ToString() + m_builderEnder.ToString();
            }

            if (IsCheckboxChecked("serie"))
            {
                sqlRequest += m_builderSerieWithPrevious.ToString();
                sqlRequest += m_builderSerieWithoutPrevious.ToString();
            }

            if (IsCheckboxChecked("team"))
            {
                sqlRequest += m_builderRequiredTeam.ToString();
            }

            if (IsCheckboxChecked("class"))
            {
                sqlRequest += m_builderRequiredClass.ToString();
            }

            return sqlRequest;
        }

        private QuestTemplateParsing m_data;

        protected SqlBuilder m_builderStarter;
        protected SqlBuilder m_builderEnder;
        protected SqlBuilder m_builderSerieWithPrevious;
        protected SqlBuilder m_builderSerieWithoutPrevious;
        protected SqlBuilder m_builderRequiredTeam;
        protected SqlBuilder m_builderRequiredClass;
    }
}

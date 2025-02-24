using Microsoft.Xrm.Sdk.Workflow;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Occurrence
{
    internal class Parser
    {
        public List<ActionParameters> ParseXML(string xmlContent)
        {
            //  XML
            XDocument xmlDoc = XDocument.Parse(xmlContent);

            // all `x:Member`
            var members = xmlDoc.Descendants().Where(e => e.Name.LocalName == "Property");
			List<ActionParameters> actionParameters = new List<ActionParameters>();
            
            // each `x:Member`
            foreach (var member in members)
            {
                ActionParameters parameterItem = new ActionParameters();
                string name = member.Attribute("Name")?.Value;
                string type = member.Attribute("Type")?.Value;
                parameterItem.Name = name;
                parameterItem.Type = type;
                // the attributes of each parameters
                var attributes = member.Descendants().Where(a => a.Name.LocalName.EndsWith("Attribute"));
                foreach (var attr in attributes)
                {
                    string attrName = attr.Name.LocalName;
                    string attrValue = attr.Attribute("Value")?.Value;

                    if (attrName == "ArgumentRequiredAttribute")
                        parameterItem.Required = Convert.ToBoolean(attrValue);
                    if (attrName == "ArgumentTargetAttribute")
                        parameterItem.IsTarget = Convert.ToBoolean(attrValue);
                    if (attrName == "ArgumentDirectionAttribute")
                        parameterItem.Direction = attrValue;
                    if (attrName == "ArgumentEntityAttribute")
                        parameterItem.EntityRef = attrValue;
                }

                actionParameters.Add(parameterItem);
            }
            return actionParameters;
        }
        public static Dictionary<string, object> ParseJsonToDictionary(string json)
        {
            try
            {
                // if escape chars exist
                string cleanedJson = json.Replace("\\\"", "\"");

                // convert to dictionary
                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(cleanedJson);
               
                if (dictionary == null)
                    throw new Exception("Failed to parse JSON.");

                return dictionary;
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>();
            }
        }
    }
}


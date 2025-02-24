using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
//using RunAction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Occurrence
{
    public class PL_Occurrence : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context =
               (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            if (context.ParentContext.Depth > 2) { return; };
            // Get a reference to the organization service.
            IOrganizationServiceFactory factory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);

            // Get a reference to the tracing service.
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            Entity entity;
            // check if is the entity contract
            if (context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity)
            {
                entity = (Entity)context.InputParameters["Target"];
                if (entity.LogicalName != "new_occurrence") return;
            }
            else
            {
                return;
            };

            /////////////////////////////////////////
            if (context.MessageName == "Create")
            {
                var action = new ActionProperties();
                OccurrenceType occurrenceType = new OccurrenceType();
                var entOccurrence = service.Retrieve("new_occurrence", context.PrimaryEntityId, new ColumnSet
    ("new_jsoninputdata"));

                if (entOccurrence is null || !entOccurrence.Contains("new_jsoninputdata"))
                    throw new Exception("♠InputData must be defined♠");

                var jsonInput = entOccurrence.GetAttributeValue<string>("new_jsoninputdata");
                // Parse Json to Dictionary
                var dicJsonParameters = Parser.ParseJsonToDictionary(jsonInput);
                if (dicJsonParameters != null) // Error Checking
                {
                    var d = dicJsonParameters.Where(a => a.Key == "occurrenceTypeId").FirstOrDefault();
                    if (string.IsNullOrEmpty(d.Key) || d.Value == null || string.IsNullOrEmpty(d.Value.ToString()))
                        throw new Exception("♠OccurrenceTypeId must be defined in json input message♠");
                    //
                    string occurrenceTypeId = d.Value.ToString();
                    occurrenceType = FindOccurrenceType(service, occurrenceTypeId);
                    if (occurrenceType == null || occurrenceType.entity == null)
                        throw new Exception("♠OccuranceType not found♠");
                    if (occurrenceType.triggerAction == Guid.Empty)
                        throw new Exception("♠Trigger Action not found♠");

                    d = dicJsonParameters.Where(a => a.Key == "targetValue").FirstOrDefault();
                    if (!string.IsNullOrEmpty(d.Key) && (d.Value == null || string.IsNullOrEmpty(d.Value.ToString())))
                        throw new Exception("♠Target must has value♠");
                    if (!string.IsNullOrEmpty(d.Key) && !occurrenceType.enableTarget)
                        throw new Exception("♠Json message has target but OccurrenceType target is not defined♠");
                    if (string.IsNullOrEmpty(d.Key) && occurrenceType.enableTarget)
                        throw new Exception("♠Json message has not target but OccurrenceType target is true♠");

                    if (occurrenceType.triggerAction != Guid.Empty)
                    {
                        action = GetActionProperties(service, occurrenceType.triggerAction);
                        if ((action.PrimaryEntity == "none" && occurrenceType.enableTarget) || (action.PrimaryEntity != "none" && !occurrenceType.enableTarget))
                            throw new Exception("♠Type of TriggerAction is not compatible by Occurrencetype♠");

                        if (action.Category != 3)
                            throw new Exception("♠Trigger Action must be CRM Action(Category 3)♠");

                        if (action.StatusCode != 2)
                            throw new Exception("♠Trigger Action must be Activate)♠");
                    }

                    if (occurrenceType.enableTarget && (string.IsNullOrEmpty(occurrenceType.targetEntity) || string.IsNullOrEmpty(occurrenceType.targetfield)))
                        throw new Exception("♠Target Entity and Field must be defined♠");


                }
                action = GetActionProperties(service, occurrenceType.triggerAction);

                try
                {
                    var Parser = new Parser();
                    List<ActionParameters> actionParameters = new List<ActionParameters>();
                    actionParameters = Parser.ParseXML(action.Xaml);
                    if (actionParameters == null)
                        throw new Exception("♠The proccess must have Input Parameter♠");

                    List<ActionParameters> inputParameters = actionParameters.Where(a => a.Direction == "Input").ToList();
                    List<ActionParameters> outputParameters = actionParameters.Where(a => a.Direction == "Output").ToList();

                    OrganizationRequest request = new OrganizationRequest(action.UniqueName);
                    var targetEntity = inputParameters.Where(a => a.IsTarget).FirstOrDefault();
                    if (action.PrimaryEntity != "none")
                    {
                        var entFindTargetRecord = FindTargetEntityRecord(service, occurrenceType.targetEntity, occurrenceType.targetfield, dicJsonParameters["targetValue"].ToString());
                        if (entFindTargetRecord == null)
                            throw new Exception("♠Target Record not found♠");
                        request["Target"] = entFindTargetRecord;
                    }

                    foreach (var inp in inputParameters)
                    {
                        if (inp.Name != "Target" && dicJsonParameters.Where(a => a.Key.ToLower() == inp.Name.ToLower()).FirstOrDefault().Key != null)
                        {
                            var jpType = dicJsonParameters[inp.Name].GetType().Name;
                            var apType = inp.Type;
                            request[inp.Name] = UnifyingType(service, jpType, inp, dicJsonParameters[inp.Name]);
                        }
                    }

                    ///current Entity Id
                    var inpSelf = inputParameters.Where(a => a.Type == "InArgument(x:String)" && a.Name == "self").FirstOrDefault();
                    if (inpSelf != null)
                        request["self"] = context.PrimaryEntityId.ToString();
                    ///
                    var response = service.Execute(request);
                    //After Successfuly Created Occurrence Record update on it
                    SuccessCreatedOccurrence(response, service, occurrenceType, context.PrimaryEntityId);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }
            if (context.MessageName=="Update")
            {
                var entOccurrence = service.Retrieve("new_occurrence", context.PrimaryEntityId, new ColumnSet
    ("statuscode", "new_jsoninputdata"));
                if (entOccurrence.GetAttributeValue<OptionSetValue>("statuscode").Value== 100000000)
                {
                    var jsonInput = entOccurrence.GetAttributeValue<string>("new_jsoninputdata");
                    var dicJsonParameters = Parser.ParseJsonToDictionary(jsonInput);
                    var d = dicJsonParameters.Where(a => a.Key == "occurrenceTypeId").FirstOrDefault();
                    string occurrenceTypeId = d.Value.ToString();
                    var occurrenceType = FindOccurrenceType(service, occurrenceTypeId);
                    if(occurrenceType.completionAction!=Guid.Empty)
                    {
                        var action = GetActionProperties(service, occurrenceType.completionAction);
                        if (action.Category==3 && action.PrimaryEntity=="none")
                            RunFinalAction(service, action.UniqueName);
                    }
                }
                    
            }
        }

        public static ActionProperties GetActionProperties(IOrganizationService service, Guid actionId)
        {
            var workflow = new ActionProperties();

            QueryExpression query = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("name", "sdkmessageid") // Selecting SdkMessage.Name
            };

            // Link with Workflow entity
            query.AddLink("workflow", "sdkmessageid", "sdkmessageid").LinkCriteria.AddCondition("workflowid", ConditionOperator.Equal, actionId);

            // Execute query
            Entity results = service.RetrieveMultiple(query).Entities.FirstOrDefault();

            if (results != null)
            {
                workflow.UniqueName = results.GetAttributeValue<string>("name");
                workflow.SdkMessageId = results.Id;
            }


            query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet("name", "category", "workflowid", "xaml", "primaryentity", "statuscode") // select workflow
            };
            query.Criteria.Conditions.Add(new ConditionExpression
            {
                AttributeName = "workflowid",
                Operator = ConditionOperator.Equal,
                Values = { actionId }
            });

            results = service.RetrieveMultiple(query).Entities.FirstOrDefault();

            if (results != null)
            {
                workflow.Name = results.GetAttributeValue<string>("name");
                workflow.Category = results.GetAttributeValue<OptionSetValue>("category").Value;
                workflow.Id = results.Id;
                workflow.Xaml = results.GetAttributeValue<string>("xaml");
                workflow.PrimaryEntity = results.GetAttributeValue<string>("primaryentity");
                workflow.StatusCode = results.GetAttributeValue<OptionSetValue>("statuscode").Value;
            }
            return workflow;
        }

        public static OccurrenceType FindOccurrenceType(IOrganizationService service, string typeId)
        {
            QueryExpression query = new QueryExpression
            {
                EntityName = "new_occurrencetype",
                ColumnSet = new ColumnSet("new_typeid", "new_triggeraction", "new_completionaction", "new_enableondeactivemode", "new_enabletarget", "new_targetentity", "new_targetfield", "statecode"),
                NoLock = true,
            };
            query.Criteria.Conditions.Add(new ConditionExpression
            {
                AttributeName = "new_typeid",
                Operator = ConditionOperator.Equal,
                Values = { typeId }
            });

            var entOccurrenceType = service.RetrieveMultiple(query).Entities.FirstOrDefault();
            OccurrenceType result = new OccurrenceType();
            if (entOccurrenceType != null)
            {
                result.entity = entOccurrenceType.ToEntityReference();
                result.typeId = entOccurrenceType.GetAttributeValue<string>("new_typeid");
                result.triggerAction = entOccurrenceType.Contains("new_triggeraction") ? entOccurrenceType.GetAttributeValue<EntityReference>("new_triggeraction").Id : Guid.Empty;
                result.completionAction = entOccurrenceType.Contains("new_completionaction") ? entOccurrenceType.GetAttributeValue<EntityReference>("new_completionaction").Id : Guid.Empty;
                result.enableOnDeactiveMode = entOccurrenceType.GetAttributeValue<bool>("new_enableondeactivemode");
                result.enableTarget = entOccurrenceType.GetAttributeValue<bool>("new_enabletarget");
                result.targetEntity = entOccurrenceType.GetAttributeValue<string>("new_targetentity");
                result.targetfield = entOccurrenceType.GetAttributeValue<string>("new_targetfield");
                result.stateCode = entOccurrenceType.GetAttributeValue<OptionSetValue>("statecode").Value;
            }
            if (result.stateCode == 1 && !result.enableOnDeactiveMode)
                return null;
            return result;
        }

        public static EntityReference FindTargetEntityRecord(IOrganizationService service, string entityName, string fieldName, string findValue)
        {
            QueryExpression query = new QueryExpression
            {
                EntityName = entityName,
                ColumnSet = new ColumnSet("createdon"),
                NoLock = true,
            };
            query.Criteria.Conditions.Add(new ConditionExpression
            {
                AttributeName = fieldName,
                Operator = ConditionOperator.Equal,
                Values = { findValue }
            });

            var result = service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (result != null)
                return result.ToEntityReference();
            else
                return null;
        }
        public static dynamic UnifyingType(IOrganizationService service,string jsonParameterType, ActionParameters actionParameter, dynamic value)
        {
            dynamic result = null;
            try
            {
                if (jsonParameterType == "String")
                {
                    switch (actionParameter.Type)
                    {
                        case "InArgument(x:String)":
                            result = value;
                            break;
                        case "InArgument(x:Int32)":
                            result = Convert.ToInt32(value);
                            break;
                        case "InArgument(x:Boolean)":
                            result = Convert.ToBoolean(value);
                            break;
                        case "InArgument(mxs:Money)":
                            result = new Money(value);
                            break;
                        case "InArgument(mxs:OptionSetValue)":
                            result = new OptionSetValue(Convert.ToInt32(value));
                            break;
                        case "InArgument(x:Double)":
                            result = Convert.ToDouble(value);
                            break;
                        case "InArgument(x:Decimal)":
                            result = Convert.ToDecimal(value);
                            break;
                        case "InArgument(s:DateTime)":
                            result = Convert.ToDateTime(value);
                            break;
                        case "InArgument(mxs:EntityReference)":
                            var ent = service.Retrieve(actionParameter.EntityRef, new Guid(value), new ColumnSet("createdon"));
                            if (ent != null)
                                result = ent.ToEntityReference();
                            else
                                result = null;
                            break;
                        default:
                            result = null;
                            break;
                    }
                }
                else
                if (jsonParameterType == "Int64")
                {
                    switch (actionParameter.Type)
                    {
                        case "InArgument(x:String)":
                            result = Convert.ToString(value);
                            break;
                        case "InArgument(x:Int32)":
                            try
                            {
                                result = Convert.ToInt32(value);
                            }
                            catch (Exception)
                            {
                                result = 0;
                            }
                            break;
                        case "InArgument(x:Boolean)":
                            result = Convert.ToBoolean(value);
                            break;
                        case "InArgument(mxs:Money)":
                            result = new Money(value);
                            break;
                        case "InArgument(mxs:OptionSetValue)":
                            result = new OptionSetValue(Convert.ToInt32(value));
                            break;
                        case "InArgument(x:Double)":
                            result = Convert.ToDouble(value);
                            break;
                        case "InArgument(x:Decimal)":
                            result = Convert.ToDecimal(value);
                            break;
                        case "InArgument(s:DateTime)":
                            result = Convert.ToDateTime(value);
                            break;
                        default:
                            result = null;
                            break;
                    }

                }
                else
                if (jsonParameterType == "Boolean")
                {
                    switch (actionParameter.Type)
                    {
                        case "InArgument(x:String)":
                            result = Convert.ToString(value);
                            break;
                        case "InArgument(x:Int32)":
                            result = Convert.ToInt32(value);
                            break;
                        case "InArgument(x:Boolean)":
                            result = Convert.ToBoolean(value);
                            break;
                        default:
                            result = null;
                            break;
                    }

                }
                else
                if (jsonParameterType == "DateTime")
                {
                    switch (actionParameter.Type)
                    {
                        case "InArgument(x:String)":
                            result = Convert.ToString(value);
                            break;
                        case "InArgument(s:DateTime)":
                            result = Convert.ToDateTime(value);
                            break;
                        default:
                            result = null;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return result;
        }

        public static void SuccessCreatedOccurrence(OrganizationResponse response, IOrganizationService service, OccurrenceType occurrenceType, Guid primaryEntityId)
        {
            try
            {
                var currEnt = service.Retrieve("new_occurrence", primaryEntityId, new ColumnSet("new_occurrenceid"));
                currEnt["new_occurrencetypeid"] = occurrenceType.typeId;
                currEnt["new_relatedtype"] = occurrenceType.entity;
                currEnt["statuscode"] = new OptionSetValue(100000000);
                StringBuilder strBuild = new StringBuilder();
                foreach (var item in response.Results)
                {
                    strBuild.AppendLine(item.Value?.ToString() + "\r\n");
                }
                currEnt["new_outputmessage"] = strBuild.ToString();
                service.Update(currEnt);
            }
            catch (Exception)
            {

            }
        }

        public static void RunFinalAction(IOrganizationService service, string actionName)
        {
            try
            {
                OrganizationRequest request = new OrganizationRequest(actionName);
                service.Execute(request);
   
            }
            catch
            {

            }

        }
    }

}

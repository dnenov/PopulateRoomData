using Autodesk.DesignScript.Runtime;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitServices.Persistence;
using System;
using System.Collections;
using System.Collections.Generic;
using Proto = Revit.Elements;

namespace DynamoAecom
{
    [Transaction(TransactionMode.Manual)]
    public class PopulateRoomData
    {
        internal PopulateRoomData()
        {

        }
        public static string Populate([DefaultArgument("{}")] IList rooms, 
            [DefaultArgument("{}")] IList<IList> points, 
            [DefaultArgument("{}")] IList elements,
            [DefaultArgument("{}")] string parameter)
        {
            int iterations = rooms.Count * elements.Count;
            int iteration = 0;

            using (AdnRme.ProgressForm form = new AdnRme.ProgressForm("Clear parameter value.", "Processing {0} out of " + elements.Count.ToString() + " elements", elements.Count))
            {
                RevitServices.Transactions.TransactionManager.Instance.EnsureInTransaction(DocumentManager.Instance.CurrentUIDocument.Document);

                foreach (var el in elements)
                {
                    Element element = ((Proto.Element)el).InternalElement;
                    element.LookupParameter(parameter).Set("");
                }

                form.Increment();
                iteration++;

                RevitServices.Transactions.TransactionManager.Instance.TransactionTaskDone();
            }

            
            using (AdnRme.ProgressForm form = new AdnRme.ProgressForm("Populating Room Data", "Processing {0} out of " + iterations.ToString() + " elements", iterations))
            {
                iteration = 0;
                RevitServices.Transactions.TransactionManager.Instance.EnsureInTransaction(DocumentManager.Instance.CurrentUIDocument.Document);

                for (int i = 0; i < elements.Count; i++)
                {
                    Element el = ((Proto.Element)elements[i]).InternalElement;
                    Parameter param = el.LookupParameter(parameter);
                    if (param == null)
                    {
                        continue;
                    }
                    foreach (var room in rooms)
                    {                    
                        if(form.getAbortFlag())
                        {
                            return "Aborted by user";
                        }

                        Autodesk.Revit.DB.Architecture.Room r = ((Proto.Element)room).InternalElement as Autodesk.Revit.DB.Architecture.Room;

                        AssignRoom(r, el, points[i], param);

                        form.Increment();
                        iteration++;
                    }
                    if (param.AsString().Equals(""))
                    {
                        param.Set("n/a");
                    }
                }                    

                RevitServices.Transactions.TransactionManager.Instance.TransactionTaskDone();
            }
            
            return String.Format("{0} elements processed successfully.", iterations.ToString());
        }
        
        private static void AssignRoom(Autodesk.Revit.DB.Architecture.Room room, Element element, IList points, Parameter parameter)
        {            
            foreach(var point in points)
            {
                if (room.IsPointInRoom(point as XYZ))
                {
                    string name = room.LookupParameter("Number").AsString() + " - " + room.LookupParameter("Name").AsString();
                    if (!parameter.AsString().Equals(""))
                    {
                        name += ", " + parameter.AsString();
                    }
                    parameter.Set(name);
                    return;
                }
            }            
        }
    }
}

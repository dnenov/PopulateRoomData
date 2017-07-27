using Autodesk.DesignScript.Runtime;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitServices.Persistence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Proto = Revit.Elements;
using DS = Autodesk.DesignScript.Geometry;
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

    [Transaction(TransactionMode.Manual)]
    public class SortViewsBySize
    {
        internal SortViewsBySize()
        {

        }
        public static List<List<View>> Sort([DefaultArgument("{}")] IList<IList> views,
            [DefaultArgument("{}")] int width,
            [DefaultArgument("{}")] int height)
        {
            int iterations = views.Count;
            double feet = 304.8;
            int wi = width;
            int he = height;

            using (AdnRme.ProgressForm form = new AdnRme.ProgressForm("Re-arranging Views", "Processing {0} out of " + iterations.ToString() + " elements", iterations))
            {
                RevitServices.Transactions.TransactionManager.Instance.EnsureInTransaction(DocumentManager.Instance.CurrentUIDocument.Document);

                List<List<View>> outlines = new List<List<View>>();
                List<View> view_set = new List<View>();

                foreach (var el in views)
                {
                    if (form.getAbortFlag())
                    {
                        return null;
                    }

                    foreach (var e in el)
                    {

                        View v = e as ViewSection;

                        double w = (v.Outline.Max.U - v.Outline.Min.U) * feet;
                        double h = (v.Outline.Max.V - v.Outline.Min.V) * feet;

                        Parameter view_comment = v.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);

                        if ((w < wi / 2) && (h < he / 2))
                        {
                            view_comment.Set('A');
                        }
                        else if ((w > wi / 2) && (w < wi) && (he < h / 4))
                        {
                            view_comment.Set('B');
                        }
                        else if ((w > wi / 2) && (w < wi) && (he < h / 2))
                        {
                            view_comment.Set('C');
                        }
                        else if ((w < wi) && (h < he))
                        {
                            view_comment.Set('D');
                        }
                        else
                        {
                            view_comment.Set('E');
                        }

                        view_set.Add(v);
                    }

                    form.Increment();

                    outlines.Add(view_set);


                    RevitServices.Transactions.TransactionManager.Instance.TransactionTaskDone();
                }

                return outlines;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ElementsOnViews
    {
        internal ElementsOnViews()
        {

        }
        public static List<List<IList<DS.Geometry>>> Intersect([DefaultArgument("{}")] IList elementGeometries,
            [DefaultArgument("{}")] IList<string> elements,
            [DefaultArgument("{}")] IList<DS.Solid> viewGeometries,
            [DefaultArgument("{}")] IList<string> viewNames
            )
        {
            int numElements = elementGeometries.Count;
            int numSections = viewGeometries.Count;

            List<List<IList<DS.Geometry>>> result = new List<List<IList<DS.Geometry>>>();

            if (numElements != elements.Count || numSections != viewNames.Count)
            {
                TaskDialog.Show("Error", "Make sure you are passing matching lists.");
                return null;
            }
            
            using (AdnRme.ProgressForm form = new AdnRme.ProgressForm("Find elements on Views.", "Processing {0} out of " + (numElements * numSections).ToString() + " elements", (numElements * numSections)))
            {
                RevitServices.Transactions.TransactionManager.Instance.EnsureInTransaction(DocumentManager.Instance.CurrentUIDocument.Document);

                // FOR EACH VIEW
                for(int i = 0;  i < numSections; i++)
                {
                    string viewName = viewNames[i];
                    DS.Solid viewSolid = viewGeometries[i];
                    List<IList<DS.Geometry>> viewList = new List<IList<DS.Geometry>>();                    

                    // FOR EACH ELEMENT
                    for (int j = 0; j < numElements; j++)
                    {
                        form.Increment();
                        if (form.getAbortFlag())
                        {
                            return null;
                        }
                        // 1 - check if the solid is not null
                        if (viewSolid == null)
                        {
                            viewList.Add(new List<DS.Geometry>());
                            continue;
                        }
                        bool test = false;
                        // 2 - check if element is in the room at all
                        if (elements[j] != null && elements[j].Contains(viewName))
                        {
                            test = true;
                        }

                        if (!test)
                        {
                            viewList.Add(new List<DS.Geometry>());
                            continue;
                        }

                        DS.Cuboid elementCube = elementGeometries[j] as DS.Cuboid;
                        // 3 - check if the cuboid is not null
                        if (elementCube == null)
                        {
                            viewList.Add(new List<DS.Geometry>());
                            continue;
                        }

                        IList<DS.Geometry> geometry = viewSolid.Intersect(elementCube);

                        //if (geometry.Count > 0)
                        //{
                        //    viewList.Add(geometry);
                        //}
                        viewList.Add(geometry);
                    }
                    result.Add(viewList);
                }
                
                RevitServices.Transactions.TransactionManager.Instance.TransactionTaskDone();
            }
            
            return result;            
        }
    }
}

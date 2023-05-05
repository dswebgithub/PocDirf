using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PocDirf.Model
{
  public class Colaborator
  {
    public string ID { get; set; }
    public string Name { get; set; }
    public string FunctionalID { get; set; }

    public string CurrentSubsidiaryName 
    { 
      get 
      {
        return CurrentSubsidiary != null ? CurrentSubsidiary.Name : string.Empty; 
      } 
    }

    public string CurrentCompanyName 
    { 
      get 
      {
        return (CurrentSubsidiary != null && CurrentSubsidiary.ParentCompany != null) ? CurrentSubsidiary.ParentCompany.Name : string.Empty; 
      } 
    }

    private Subsidiary _currentSubsidiary;
    private List<InstanceSubsidiary> _subsidiaries;
    
    public Subsidiary CurrentSubsidiary { 
      get {
        if (_currentSubsidiary == null)
        {
          //if (Subsidiaries != null)
          //{
          //  Subsidiary currentSubsidiary = null;
          //  foreach (var temp in Subsidiaries)
          //  {
          //    if (currentSubsidiary == null || temp.InitialDate > currentSubsidiary.InitialDate)
          //      currentSubsidiary = temp;
          //  }

          //  _currentSubsidiary = currentSubsidiary;
          //}
          if (HireDateSubsidiaries != null)
          {
            var maxDate = HireDateSubsidiaries.Keys.Max();
            _currentSubsidiary = HireDateSubsidiaries[maxDate];
          }

        }
        return _currentSubsidiary;
      } 
    }
    
    
    public List<InstanceSubsidiary> Subsidiaries
    {
      get
      {
        if (_subsidiaries == null)
        {
          if (HireDateSubsidiaries != null)
          {
            var result = new List<InstanceSubsidiary>();
            foreach (var item in HireDateSubsidiaries)
            {
              result.Add(new InstanceSubsidiary(item.Key, item.Value));
            }
            _subsidiaries = result;
          }
        }
        return _subsidiaries;
      }
    }
    public Dictionary<InstanceSubsidiaryData, Subsidiary> HireDateSubsidiaries { get; set; }

    public List<PdfData> Pdfs { get; set; }

    public bool HasPdf
    {
      get
      {
        return (Pdfs != null && Pdfs.Count > 0);
      }
    }
  }

  public struct InstanceSubsidiaryData : IComparable<InstanceSubsidiaryData>, IComparable
  {
    public string FunctionalID{get;set;}
    public DateTime HireDate {get;set;}
  
    public int  CompareTo(InstanceSubsidiaryData other)
    {
 	    return this.HireDate.CompareTo(other.HireDate);
    }

    public int CompareTo(object obj)
    {
 	    var comp = (InstanceSubsidiaryData)obj;
      return CompareTo(comp);
    }
  }

  public class LegalEntity
  {
    public string ID { get; set; }
    public string Name { get; set; }
    public string LegalName { get; set; }

    public override string ToString()
    {
      return this.Name??string.Empty;
    }
  }

  public class Subsidiary: LegalEntity
  {
    public Company ParentCompany { get; set; }
  }

  public class InstanceSubsidiary : Subsidiary
  {
    public InstanceSubsidiary(InstanceSubsidiaryData data, Subsidiary subsidiary)
    {
      FunctionalID = data.FunctionalID;
      HireDate = data.HireDate;
      Subsidiary = subsidiary;
    }

    public string FunctionalID { get; private set; }
    public DateTime HireDate { get; private set; }
    public Subsidiary Subsidiary { get; private set; }


    public new string ID { get { return Subsidiary.ID; } }
    public new string Name { get { return Subsidiary.Name; } }
    public new string LegalName { get { return Subsidiary.LegalName; } }

    public new Company ParentCompany
    {
      get
      {
        if (Subsidiary != null)
          return Subsidiary.ParentCompany;
        else
          return null;
      }
    }
  }

  public class Company:LegalEntity
  {
    public List<Subsidiary> Subsidiaries { get; set; }
  }


}

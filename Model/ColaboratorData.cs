using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FileHelpers;
using System.ComponentModel;


namespace PocDirf.Model
{
  [DelimitedRecord(";")]
  [IgnoreFirst(1)]
  public class ColaboratorData : IComparable<ColaboratorData>, IComparable
  {
    [FieldTrim(TrimMode.Both)]
    protected string _id;
    [FieldTrim(TrimMode.Both)]
    protected string _functionalID;
    [FieldTrim(TrimMode.Both)]
    protected string _name;
    [FieldTrim(TrimMode.Both)]
    protected string _subsidiaryID;
    [FieldTrim(TrimMode.Both)]
    protected string _subsidiaryName;
    [FieldConverter(ConverterKind.Date, "yyyy-MM-dd hh:mm:ss.fff")]
    protected DateTime _hireDate;
    [FieldTrim(TrimMode.Both)]
    protected string _campanyID;
    [FieldTrim(TrimMode.Both)]
    protected string _companyName;

    [FieldIgnored]
    private List<PdfData> _pdfData;

    [Browsable(true)]
    public List<PdfData> PdfData
    {
      get { return _pdfData; }
      set { _pdfData = value; }
    }

    public bool HasPdfData
    {
      get { return _pdfData != null && _pdfData.Count > 0; }
    }


    public string ID
    {
      get { return _id; }
      set { _id = value; }
    }

    public string FunctionalID
    {
      get { return _functionalID; }
      set { _functionalID = value; }
    }

    public string Name
    {
      get { return _name; }
      set { _name = value; }
    }

    public string SubsidiaryID
    {
      get { return _subsidiaryID; }
      set { _subsidiaryID = value; }
    }

    public string SubsidiaryName
    {
      get { return _subsidiaryName; }
      set { _subsidiaryName = value; }
    }

    public DateTime HireDate
    {
      get { return _hireDate; }
      set { _hireDate = value; }
    }

    public string CampanyID
    {
      get { return _campanyID; }
      set { _campanyID = value; }
    }

    public string CompanyName
    {
      get { return _companyName; }
      set { _companyName = value; }
    }


    public string UniqueID
    {
      get { return string.Format("{0}_{1}", _id, _campanyID); }
    }

    public string UniqueIDTemp
    {
      get { return string.Format("{0}_{1}_{2}", _id, _campanyID, _subsidiaryID); }
    }

    public int CompareTo(object obj)
    {
      ColaboratorData data = obj as ColaboratorData;
      if (data == null)
        throw new Exception("Erro de comparação. O objeto deve ser um ColaboratorData");

      return this.CompareTo(data);
    }

    public int CompareTo(ColaboratorData other)
    {
      return this.UniqueID.CompareTo(other.UniqueID);
    }


  }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace PocDirf.Model
{
  [Serializable]
  public class PdfData
  {
    public string UniqueID { get { return string.Format("{0}_{1}_{2}", ColaboratorID, CompanyID, PdfType); } }
    public string FunctionalUniqueID { get { return string.Format("{0}_{1}_{2}_{3}", ColaboratorID, CompanyID, ColaboratorFunctionalID, PdfType); } }

    public string CompanyID { get; set; }
    public string ColaboratorID { get; set; }
    public string ColaboratorFunctionalID { get; set; }

    public string File { get; set; }
    public string OutputFile { get; set; }
    public int InitialPage { get; set; }
    public int NumberOfPages { get; set; }

    public string Message { get; set; }

    public PdfType PdfType { get; set; }
  }

  public enum PdfType
  {
    [Description("Dirf")]
    Dirf,   
    
    [Description("Detalhes de Convênio")]
    MedicalDetail,

    [Description("Desconhecido")]
    Unknow
  }

}

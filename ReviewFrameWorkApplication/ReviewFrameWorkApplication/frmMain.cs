using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace lsi
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Index the documents
        /// </summary>
        private void IndexThings()
        {
            lsiReader oReader = new lsiReader();
            oReader.IndexDirectory(LSICommon.Instance.LSIConfig.document_directory);


            // The base part. Create the Term Document Matrix. It is also 
            DotNetMatrix.GeneralMatrix oLocalWeights = new DotNetMatrix.GeneralMatrix(oReader.WordList.Count, oReader.DocList.Count);
            DocWordRelation oDocWord;
            foreach (DictionaryEntry oRow in oReader.WordList)
            {
                foreach (DictionaryEntry oCol in oReader.DocList)
                {
                    oDocWord.DocID = (int)oCol.Key;
                    oDocWord.WordID = (int)oRow.Value;

                    if (oReader.DocWordRelation.ContainsKey(oDocWord))
                    {
                        oLocalWeights.Array[(int)oRow.Value][(int)oCol.Key] = (int)oReader.DocWordRelation[oDocWord];
                    }
                    else
                    {
                        oLocalWeights.Array[(int)oRow.Value][(int)oCol.Key] = 0;
                    }
                }
            }

            // Term Document matrix calculated. 

            // NOTE: You can save this TDM here to avoid recalculations 
            //         OR save the decomposed & reduced matrices later (I did it later)


            //calculate term weights

            double[] oGlobalTermWeights = new double[oReader.WordList.Count];
            double[] oDocNormFactors = new double[oReader.DocList.Count];

            //Calculating global weights -> Gi
            for (int i = 0; i < oLocalWeights.Array.Length; i++)
            {
                int sum = 0;
                double Fi = 0;
                for (int j = 0; j < oLocalWeights.Array[i].Length; j++)
                {
                    if (oLocalWeights.Array[i][j] > 0)
                    {
                        sum+=1;
                        Fi += oLocalWeights.Array[i][j];
                    }   
                }
                if (sum > 0) oGlobalTermWeights[i] = Math.Log(oReader.DocList.Count/ (double)sum);
                else oGlobalTermWeights[i] = 0;

                // The following commented section is another way of weighting the terms
                //if (sum > 0) oGlobalTermWeights[i] = Math.Sqrt((Fi / sum) - 0.9);
                //else oGlobalTermWeights[i] = 0;
            }

            //COMMENTED: Only meant for testing use.
            //Forcing global weights to 1;
            //for (int i = 0; i < oGlobalTermWeights.Length; i++) oGlobalTermWeights[i] = 1;


            //Calculating normalization factors-> Nj. Cosine normalization factor
            for (int j = 0; j < oLocalWeights.Array[0].Length; j++)
            {
                double sum = 0;
                for (int i = 0; i < oLocalWeights.Array.Length; i++)
                {
                    sum += (oGlobalTermWeights[i] * oLocalWeights.Array[i][j])
                                * (oGlobalTermWeights[i] * oLocalWeights.Array[i][j]);
                }
                oDocNormFactors[j] = 1 / Math.Sqrt(sum);
                
            }


            //COMMENTED: Only meant for testing use.
            //Forcing normalization to 1;
            //for (int i = 0; i < oDocNormFactors.Length; i++) oDocNormFactors[i] = 1;


            //Finally weighting terms and creating a weighted term document matrix
            DotNetMatrix.GeneralMatrix oWTDM = new DotNetMatrix.GeneralMatrix(oReader.WordList.Count, oReader.DocList.Count);

            for (int i = 0; i < oWTDM.Array.Length; i++)
            {
                for (int j = 0; j < oWTDM.Array[i].Length; j++)
                {
                    oWTDM.Array[i][j] = oLocalWeights.Array[i][j] * oGlobalTermWeights[i] * oDocNormFactors[j];
                }
            }


            //end of calculate term weights

            // Calculate SVD
            DotNetMatrix.SingularValueDecomposition svd = new DotNetMatrix.SingularValueDecomposition(oWTDM);


            // Set the svd rank. This will reduce the dimensions in the term document matrix
            int svd_rank = (int)(oReader.DocList.Count*( (double)LSICommon.Instance.LSIConfig.k_percent_for_rank_approx /100));
            if (svd_rank == 0 || svd_rank > oReader.DocList.Count) svd_rank = oReader.DocList.Count;

            // Reduce the vector holding singular values (Sk)
            DotNetMatrix.GeneralMatrix sk = new DotNetMatrix.GeneralMatrix(svd_rank, svd_rank);
            for (int i = 0; i < svd_rank; i++)
            {
                for (int j = 0; j < svd_rank; j++)
                {
                    sk.Array[i][j] = svd.S.Array[i][j];
                }
            }

            // Calculate Sk inverse
            DotNetMatrix.GeneralMatrix skinv = sk.Inverse();

            // Calculate U
            DotNetMatrix.GeneralMatrix u = svd.GetU(); // U

            // Calculate the reduced Uk 
            DotNetMatrix.GeneralMatrix uk = new DotNetMatrix.GeneralMatrix(u.Array.Length, svd_rank);
            for (int i = 0; i < u.Array.Length; i++)
            {
                for (int j = 0; j < svd_rank; j++)
                {
                    uk.Array[i][j] = u.Array[i][j];
                }
            }

            #region [Save the index]
            // Save the index
            // Only the necessary values required by the query have been saved. 
            // Can store the original TDM for hybrid querying (That is not the purpose in part 1)

            DocIndex oDocIndex = new DocIndex();
            oDocIndex.WordList = new HashObj[oReader.WordList.Count];
            oDocIndex.DocList = new HashObj[oReader.DocList.Count];
            //o.DocWord = new HashObj[oReader.DocWordRelation.Count];

            foreach (DictionaryEntry oEntry in oReader.WordList)
            {
                oDocIndex.WordList[(int)oEntry.Value].key = oEntry.Key;
                oDocIndex.WordList[(int)oEntry.Value].value = oEntry.Value;
            }

            foreach (DictionaryEntry oEntry in oReader.DocList)
            {
                oDocIndex.DocList[(int)oEntry.Key].key = oEntry.Key;
                oDocIndex.DocList[(int)oEntry.Key].value = oEntry.Value;
            }

            oDocIndex.skinv = skinv.Array;
            oDocIndex.uk = uk.Array;
            oDocIndex.WTDM = oWTDM.Array;

            BinaryFormatter bf = new BinaryFormatter();
            FileStream f = File.Open(LSICommon.Instance.LSIAppPath + @"\index.bin", FileMode.Create);
            bf.Serialize(f, oDocIndex);
            f.Close();

            //End of saving index

            #endregion
        }

        /// <summary>
        /// Find the documents related to the query
        /// </summary>
        private void FindThings()
        {

            // Declaring variables required for calculations
            Hashtable WordList = new Hashtable();
            Hashtable DocList = new Hashtable();
            DotNetMatrix.GeneralMatrix uk=null;
            DotNetMatrix.GeneralMatrix skinv = null;
            DotNetMatrix.GeneralMatrix oWTDM = null;

            DocIndex oDocIndex;
            oDocIndex.DocList = null;
            oDocIndex.WordList= null;
            oDocIndex.uk = null;
            oDocIndex.WTDM = null;
            oDocIndex.skinv = null;

            // Read and load the index document
            string index_path = LSICommon.Instance.LSIAppPath + @"\index.bin";
            bool bRead=false;
            BinaryFormatter bf = new BinaryFormatter();
            if (File.Exists(index_path))
            {
                try
                {
                    FileStream f = File.Open(LSICommon.Instance.LSIAppPath + @"\index.bin", FileMode.Open);
                    oDocIndex = (DocIndex)bf.Deserialize(f);
                    f.Close();
                    bRead = true;
                }
                catch
                {
                    bRead = false;
                }
            }
            else
            {
                bRead = false;
            }

            if (!bRead)
            {
                MessageBox.Show("Index not created. Please index the documents");
                return;
            }


            // Restore the wordlist
            for (int i = 0; i < oDocIndex.WordList.Length; i++)
            {
                WordList.Add(oDocIndex.WordList[i].key, oDocIndex.WordList[i].value);
            }

            // Restore the documentlist
            for (int i = 0; i < oDocIndex.DocList.Length; i++)
            {
                DocList.Add(oDocIndex.DocList[i].key, oDocIndex.DocList[i].value);
            }

            // Restore the other SVD derived vectors and weighted TDM
            uk = new DotNetMatrix.GeneralMatrix( oDocIndex.uk);
            skinv = new DotNetMatrix.GeneralMatrix(oDocIndex.skinv);
            oWTDM = new DotNetMatrix.GeneralMatrix(oDocIndex.WTDM);


            // Get the query
            string newquery = txtQuery.Text;
            string[] newqwords = LSICommon.Instance.GetWords(newquery);

            // Create the query vector
            DotNetMatrix.GeneralMatrix qt = new DotNetMatrix.GeneralMatrix(1, WordList.Count);
            for (int i = 0; i < WordList.Count; i++) qt.Array[0][i] = 0;

            // Apply stemming to the query vector
            PorterStemmer oStemmer = new PorterStemmer();
            for (int i = 0; i < newqwords.Length; i++)
            {
                newqwords[i] = oStemmer.stemTerm(newqwords[i]);
                if (WordList.ContainsKey(newqwords[i]))
                {
                    qt.Array[0][(int)WordList[newqwords[i]]] += 1;
                }
            }

            // Normalizing the query vector
            double qtmax = 0;
            for (int i = 0; i < qt.Array[0].Length; i++) if (qt.Array[0][i] > qtmax) qtmax = qt.Array[0][i];

            double[] simarray = new double[DocList.Count];


            if (qtmax != 0)
            {
                for (int i = 0; i < qt.Array[0].Length; i++)
                {
                    qt.Array[0][i] = (qt.Array[0][i] / qtmax);
                }

                // Calculate the resultant query vector 
                DotNetMatrix.GeneralMatrix qt_uk = qt.Multiply(uk); // qT * Uk
                DotNetMatrix.GeneralMatrix q = qt_uk.Multiply(skinv); // Resultant q

                // Calculate similarities now ( with each derived document vector)
                for (int l = 0; l < DocList.Count; l++)
                {
                    DotNetMatrix.GeneralMatrix d_v = new DotNetMatrix.GeneralMatrix(WordList.Count, 1);
                    for (int k = 0; k < WordList.Count; k++)
                    {
                        d_v.Array[k][0] = oWTDM.Array[k][l];
                    }

                    DotNetMatrix.GeneralMatrix dt = d_v.Transpose(); // dT 
                    DotNetMatrix.GeneralMatrix dt_uk = dt.Multiply(uk); // dT * uk
                    DotNetMatrix.GeneralMatrix d = dt_uk.Multiply(skinv); // Resultant d

                    // Calculate cosine similarity:
                    simarray[l] = 0;
                    simarray[l] = calculate_cosine_sim(q.Array[0], d.Array[0]);
                }

            }
            else
            {
                MessageBox.Show("No results found");
                // Since qtmax is zero, the query vector is null. This implies that there can be no results
            }

            // Display the results in datagrid
            DataTable oResTable = new DataTable();
            DataRow oResDR = null;
            oResTable.Columns.Add("filename", typeof(string));
            oResTable.Columns.Add("val", typeof(double));
            for (int i = 0; i < simarray.Length; i++)
            {
                oResDR = oResTable.NewRow();

                oResDR["val"] = (Math.Round(simarray[i]*100, 6));
                oResDR["filename"] = DocList[i].ToString();

                oResTable.Rows.Add(oResDR);
            }
            dataGridView1.DataSource = oResTable;

            // Sort the results in descending order
            dataGridView1.Sort(dataGridView1.Columns["val"], ListSortDirection.Descending);

            // Formatting the results
            dataGridView1.Columns["val"].HeaderText = "Rating/100";
            dataGridView1.Columns["filename"].HeaderText = "Document path";
            dataGridView1.Columns["val"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dataGridView1.Columns["filename"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;


            dataGridView1.RowHeadersVisible = false;
            DataGridViewCellStyle oCellStyle = new DataGridViewCellStyle();
            oCellStyle.BackColor = Color.AliceBlue;
            dataGridView1.AlternatingRowsDefaultCellStyle = oCellStyle;


            try
            {
                foreach (DataGridViewRow oRow in dataGridView1.Rows)
                {
                    oRow.Cells["filename"].ToolTipText = ReadShortTextContent(oRow.Cells["filename"].Value.ToString().Trim());
                }
            }
            catch {
                //Supress any exception here
            }

        }

        // Calculate the cosine similarity between two vectors
        private double calculate_cosine_sim(double[] q, double[] d)
        {
            if (q.Length != d.Length) throw new Exception("both vectors must be of similar length");
            // Calculate cosine similarity:
            double numer = 0;
            double denom = 1;
            double d1=0, d2=0;
            for (int m = 0; m < q.Length; m++)
            {
                numer += (q[m] * d[m]);
                d1 += (q[m] * q[m]);
                d2 += (d[m] * d[m]);
            }

            denom = Math.Sqrt(d1) * Math.Sqrt(d2);
            return numer/ denom;
        }

        // Search similar text
        private void btnSearch_Click(object sender, EventArgs e)
        {
            //WARNING: No exception handling done in this code (since this will be subject to huge changes later)
            try
            {
                //IndexThings();
                this.FindThings();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception occurred \n" + ex.Message + "\n" + ex.StackTrace);
            }
        }

        //Configuration form
        private void btnConfigure_Click(object sender, EventArgs e)
        {
            Form f = new frmConfigure();
            f.ShowDialog();
        }

        // Index the documents
        private void btnIndex_Click(object sender, EventArgs e)
        {
            this.Hide();
            DialogResult oDialogResult=MessageBox.Show("Click OK to start indexing. The form will re-appear after indexing completes","",MessageBoxButtons.OKCancel);
            if (oDialogResult == DialogResult.Cancel)
            {
                this.Show();
                return;
            }
            //WARNING: No exception handling done in this code (since this will be subject to huge changes later)
            try
            {
                this.IndexThings();
                MessageBox.Show("Indexing complete");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception occurred \n" + ex.Message + "\n" + ex.StackTrace);
            }
            this.Show();
        }

        // Read some initial content of the text file
        private string ReadShortTextContent(string file)
        {
            string sText = "";
            try
            {
                TextReader oTR = File.OpenText(file);
                sText = oTR.ReadToEnd();
                oTR.Close();
                oTR = null;

                string[] s = sText.Split();
                sText = "";
                for(int i=0;i<20;i++)
                {
                    sText += (s[i] + " ");
                }
                sText += "...";
            }
            catch { 
                //Supressing any exception here 
            }
            return sText;
        }

        // Open the file on double click
        private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                string ofile = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();
                if (File.Exists(ofile))
                {
                    System.Diagnostics.Process proc = new System.Diagnostics.Process();
                    proc.EnableRaisingEvents = false;
                    proc.StartInfo.FileName = ofile;
                    proc.Start();
                }
            }
            catch
            {
                //Supressing any exception here 
            }
        }
    }
}
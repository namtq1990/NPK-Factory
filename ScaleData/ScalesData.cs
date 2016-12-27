﻿using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using ScalesData;
using System.IO;
using System.Data.SqlServerCe;
using log4net;

//[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace ScalesData
{

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None),
        Guid("B5B09ED9-933F-41A5-8815-02747B4E1682"),
        ProgId("ScalesData.Launcher")]
    public class Launcher : IHelper
    {
        public const string TABLE_SCALES = "Scales";
        public const string TABLE_SCALES_VALUE = "ScalesValue";

        public const string COL_SCALES_TAG = "Tag";
        public const string COL_SCALES_TIME = "Time";
        public const string COL_SCALES_WEIGHT = "Weight";

        private static SqlCeConnection mConnection;
        private static SqlCeCommand mCmd;

        static Launcher()
        {
            mConnection = new SqlCeConnection();
            mCmd = new SqlCeCommand();

            Logger.Log("Create new connection at domain: " + AppDomain.CurrentDomain);
            AppDomain.CurrentDomain.ProcessExit += onApplicationExit;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        public void open()
        {
            try
            {
                if (!isConnecting())
                {
                    string connectStr;// = "Data Source=(localdb)\\ProjectsV13;Initial Catalog=NBFactory;Integrated Security=True;Pooling=False;Connect Timeout=30;";
                                      //connectStr = "Data Source=ASROCK-PC\\WINCC;Initial Catalog=NBFactory;Integrated Security=True;";
                    string pathDB = DataStorage.getFilePath(DataFile.Database);
                    if (!File.Exists(pathDB))
                    {
                        pathDB = "|DataDirectory|\\NBFactory.sdf";
                    }

                    connectStr = string.Format("Data Source=" + pathDB);
                    //connectStr += "MultipleActiveResultSets=true;";

                    Logger.Log(connectStr);

                    mConnection.ConnectionString = connectStr;
                    mConnection.Open();
                    mCmd.Connection = mConnection;
                }
                else
                {
                    Logger.Log("Still connected to database at " + mConnection.ConnectionString);
                }

                Logger.Log("DB Opened");
            } catch (Exception e)
            {
                string message = "Can't load database file. Please select right data folder. Data folder must contain "
                    + DataStorage.getFileName(DataFile.Database) + ", make sure that database file is readable and writable.";
                Logger.Log(message);
                MessageBoxEx.Show(message);
                ExportExcelForm.showSelectDatabaseDialog(true);
            }
        }

        public void close()
        {
            mConnection.Close();
            Logger.Log("DB Closed");
        }

        public void exit()
        {
            if (System.Windows.Forms.Application.MessageLoop)
            {
                // WinForms app
                System.Windows.Forms.Application.Exit();
            }
            else
            {
                // Console app
                System.Environment.Exit(1);
            }
        }

        public static SqlCeConnection getConnection()
        {
            return mConnection;
        }

        public bool isConnecting()
        {
            return mConnection.State == System.Data.ConnectionState.Open;
        }

        public void setQuery(String query)
        {
            Logger.Log("Try to query: " + query);
            mCmd.CommandText = query;
            mCmd.Parameters.Clear();
        }

        public void showView()
        {
            showView(false);
        }

        public void showView(bool autoConnect = false)
        {
            System.Windows.Forms.Form form = createForm();
            if (autoConnect)
            {
                open();
                form.FormClosed += Form_FormClosed;
            }

            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.Run(form);
        }

        private void Form_FormClosed(object sender, System.Windows.Forms.FormClosedEventArgs e)
        {
            close();
        }

        public System.Windows.Forms.Form createForm()
        {
            return new ExportExcelForm(this);
        }

        public void exportExcel(string _startDate, string _endDate, string password = "")
        {
            ExcelExporter exporter = new ExcelExporter();
            List<ScalesValue> data = getScalesValue(_startDate, _endDate);

            exporter.DataList = data;
            exporter.ScalesList = getAllScales();
            if (_startDate != null)
            {
                exporter.StartDate = DateTime.Parse(_startDate);
            } else if (data.Count > 0)
            {
                exporter.StartDate = data[0].Time;
            }
            if (_endDate != null)
            {
                exporter.EndDate = DateTime.Parse(_endDate);
            } else
            {
                exporter.EndDate = DateTime.Now;
            }

            exporter.Password = password;

            exporter.export();
        }

        public void addScales(string tag)
        {
            //try
            //{
            string query = string.Format("select * from {0} where {1}=@tag", TABLE_SCALES, COL_SCALES_TAG);
            setQuery(query);
            mCmd.Parameters.AddWithValue("@tag", tag);
            if (!mCmd.ExecuteReader().Read())
            {
                query = String.Format("insert into {0} values(@tag)", TABLE_SCALES);
                //SqlCeCommand cmd = new SqlCeCommand(query, mConnection);
                setQuery(query);
                mCmd.Parameters.AddWithValue("@tag", tag);

                mCmd.ExecuteNonQuery();
            }
            //}
            //catch (SqlException e)
            //{
            //    if (e.Number == ErrorNumber.SQL_DUPLICATE_KEY)
            //    {
            //        return;
            //    }

            //    throw e;
            //}
        }

        public string[] getAllScales()
        {
            List<string> lstScales = new List<string>();
            String query = string.Format("select {0} from {1}", COL_SCALES_TAG, TABLE_SCALES);
            //SqlCeCommand cmd = new SqlCeCommand(query, mConnection);
            setQuery(query);
            System.Data.Common.DbDataReader reader = mCmd.ExecuteReader();

            while (reader.Read())
            {
                string tag = reader[COL_SCALES_TAG].ToString();
                lstScales.Add(tag);
            }

            reader.Close();
            return lstScales.ToArray();
        }

        public void insertScalesValue(string scaleTag, float weight)
        {
            DateTime time = DateTime.Now;
            time.AddMilliseconds(-time.Millisecond);

            insertScalesValue(time.ToString(), scaleTag, weight);
        }

        public void insertScalesValue(string datetime, string scaleTag, float weight)
        {
            string query = String.Format("insert into {0} (Time, Weight, Tag) values(@time, @weight, @tag)", TABLE_SCALES_VALUE);
            //SqlCeCommand cmd = new SqlCeCommand(query, mConnection);
            setQuery(query);

            mCmd.Parameters.AddWithValue("@table", TABLE_SCALES_VALUE);
            mCmd.Parameters.AddWithValue("@time", datetime);
            mCmd.Parameters.AddWithValue("@weight", weight);
            mCmd.Parameters.AddWithValue("@tag", scaleTag);

            mCmd.ExecuteNonQuery();
        }

        ///<summary>
        ///Return scalesValue from database from <paramref name="fromDate"/> to <paramref name="toDate"/>
        ///</summary>
        ///<param name="fromDate">from Date, if null will get all value until toDate</param>
        ///<param name="toDate">take until Date, if null will get all value after fromDate</param>
        public List<ScalesValue> getScalesValue(string fromDate, string toDate)
        {
            string condition = null;
            if (fromDate != null)
            {
                condition = " where Time >= @fromDate";
            }
            if (toDate != null)
            {
                if (condition == null)
                    condition = " where ";
                else
                    condition += " and ";
                condition += "Time <= @toDate";
            }

            string query = String.Format("select {0}, {1}, {2} from {3} {4} order by {5}",
                COL_SCALES_TIME,
                COL_SCALES_WEIGHT,
                COL_SCALES_TAG,
                TABLE_SCALES_VALUE,
                condition,
                COL_SCALES_TIME);
            //SqlCeCommand cmd = new SqlCeCommand(query, mConnection);
            setQuery(query);
            if (fromDate != null) mCmd.Parameters.AddWithValue("@fromDate", DateTime.Parse(fromDate));
            if (toDate != null) mCmd.Parameters.AddWithValue("@toDate", DateTime.Parse(toDate));

            System.Data.Common.DbDataReader reader = mCmd.ExecuteReader();
            List<ScalesValue> list = new List<ScalesValue>();

            while (reader.Read())
            {
                ScalesValue value = new ScalesValue(reader.GetDateTime(0), 
                    (float) reader.GetDouble(1),
                    reader.GetString(2));

                list.Add(value);
            }

            reader.Close();
            return list;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBoxEx.Show("Oop! Something happened. Application will be exited.");
            Logger.Log(e.ExceptionObject.ToString());
            Environment.Exit(1);
        }

        private static void onApplicationExit(object sender, EventArgs e)
        {
            new Launcher().close();
        }
    }

    public class ScalesValue
    {
        public ScalesValue() { }

        public ScalesValue(DateTime time, float weight, string tag)
        {
            Time = time;
            Weight = weight;
            ScaleTag = tag;
        }

        public DateTime Time { get; set; }

        public float Weight { get; set; }

        public string ScaleTag { get; set; }
    }

    public class Logger
    {
        private static readonly ILog logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName);

        static Logger()
        {
            //log4net.Config.BasicConfigurator.Configure();
            string directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string configFile = Path.Combine(directory, "NBFactory.config");
            FileInfo file = new FileInfo(configFile);
            log4net.Config.XmlConfigurator.Configure(file);
        }

        public static void Log(string line)
        {
            logger.Info(line);
            //Console.WriteLine(line);
        }

    }

    public class ErrorNumber
    {
        public const int SQL_DUPLICATE_KEY = 2627;
    }
}
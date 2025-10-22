using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    internal class ScanAnalysis
    {
        StatusMessage statusMessage;
        string baseDir;
        string corePath;
        public ScanAnalysis(StatusMessage statusMessage)
        {
            this.statusMessage = statusMessage;
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            corePath = Path.Combine(baseDir, @"..\..\Core");
            corePath = Path.GetFullPath(corePath);
        }
        public void Generate2DScanMap(string filePath)
        {
            string scriptPath = Path.Combine(corePath, "ScanAnalysis_2DMap.py");
            string cmd = scriptPath + " --file " + filePath;
            Process process = new Process();
            process.StartInfo.FileName = "python.exe";
            process.StartInfo.Arguments = cmd;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.ErrorDataReceived += Process_ErrorDataReceived;
            process.OutputDataReceived += Process_OutputDataReceived;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }

        public void Generate1DScanMap(string filePath, bool executeCurveFit)
        {
            string scriptPath = Path.Combine(corePath, "ScanAnalysis_1DMap.py");
            string cmd = scriptPath + " --file " + filePath;
            if (executeCurveFit)
            {
                cmd += " --fit";
            }
            Process process = new Process();
            process.StartInfo.FileName = "python.exe";
            process.StartInfo.Arguments = cmd;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.ErrorDataReceived += Process_ErrorDataReceived;
            process.OutputDataReceived += Process_OutputDataReceived;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }


        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                statusMessage.CreateStatusMessage(e.Data.ToString());
                Logger.Instance.Log(e.Data.ToString(), LogType.Warning);
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if(e.Data != null)
            {
                statusMessage.CreateStatusMessage(e.Data.ToString());
                Logger.Instance.Log(e.Data.ToString(), LogType.Warning);
            }
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Xml.Linq;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FilesUploadApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private IConfiguration config;
        private string rootPath
        {
            get
            {
                return config["config:rootPath"];
            }
        }
        private long maxFileSize
        {
            get
            {
                return 1024 * 1024 * int.Parse(config["config:maxFileSize"]);
            }
        }
        private long blockSize
        {
            get
            {
                return 1024 * 1024 * int.Parse(config["config:blockSize"]);
            }
        }
        public UploadController(IConfiguration cfg)
        {
               config = cfg;
        }

        //POST api/<UploadController>
        [HttpPost]
        public ActionResult<UploadTask> RequestUpload([FromBody] FileInfo fileInfo)
        {
            UploadTask task = new UploadTask();
            task.hash = fileInfo.GetMD5();
            if(fileInfo.size > this.maxFileSize)
            {
                task.error = $"文件过大, 超过设定值: {this.maxFileSize} (bytes)";
                return this.BadRequest(task.error);
            }
            if(!this.PrepareNextTask(task,fileInfo))
            {
                return this.BadRequest(task.error);
            }
            return this.Ok(task);
        }

        private bool PrepareNextTask(UploadTask task,FileInfo fileInfo)
        {
            var path = Path.Combine(this.rootPath, task.hash);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                long blockCount = fileInfo.size / (this.blockSize);
                for(int i=0;i< blockCount;i++)
                {
                    var fName = string.Format("{0}.task", i.ToString("X8"));
                    var obj = new {
                        begin = i * this.blockSize,
                        length = this.blockSize,
                    };
                    var tj = JsonConvert.SerializeObject(obj);
                    System.IO.File.WriteAllText(Path.Combine(path, fName),tj,System.Text.Encoding.UTF8);
                }
                long lastLength = fileInfo.size - blockCount * this.blockSize;
                if(lastLength > 0)
                {
                    var fName = string.Format("{0}.task", blockCount.ToString("X8"));
                    var obj = new
                    {
                        begin = blockCount * this.blockSize,
                        length = lastLength
                    };
                    var tj = JsonConvert.SerializeObject(obj);
                    System.IO.File.WriteAllText(Path.Combine(path, fName), tj, System.Text.Encoding.UTF8);
                }
            }
            var files = Directory.GetFiles(path, "*.task");
            if(files == null ||  files.Length == 0)
            {
                task.end = true;
                return true;
            }
            List<string> fList = new List<string>(files);
            fList.Sort();
            var json = System.IO.File.ReadAllText(fList[0]);
            UploadTask fTask = JsonConvert.DeserializeObject<UploadTask>(json);
            task.begin = fTask.begin;
            task.length = fTask.length;
            task.block = System.IO.Path.GetFileNameWithoutExtension(fList[0]);
            task.end = false;
            return true;
        }

        [HttpPost]
        public ActionResult SendBlock([FromBody] FileSegment seg)
        {
            if (seg == null)
            {
                return this.BadRequest(new
                {
                    result = false,
                    error = "片段不能为空"
                });
            }
            if(string.IsNullOrEmpty(seg.hash) || string.IsNullOrEmpty(seg.block))
            {
                return this.BadRequest(new
                {
                    result = false,
                    error = "文件哈希&文件块不能为空"
                });
            }
            try
            {
                //写入文件, 删除task文件
                var dataFile = Path.Combine(this.rootPath, seg.hash, string.Format("{0}.data", seg.block));
                System.IO.File.WriteAllText(dataFile, JsonConvert.SerializeObject(seg));
                var taskFile = Path.Combine(this.rootPath, seg.hash, string.Format("{0}.task", seg.block));
                System.IO.File.Delete(taskFile);
                return this.Ok(new
                {
                    result = true,
                    error = string.Empty
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return this.BadRequest(new
                {
                    result = false,
                    error = ex.Message
                });
            }
        }
    }

    public class UploadTask
    {
        public bool end { get; set; }
        public string error { get; set; }
        public string hash { get; set; }
        public string block { get; set; }
        public int begin { get; set; }
        public int length { get; set; }
    }

    public class FileInfo
    {
        public string name { get; set; }
        public long size { get; set; }

        public string GetMD5()
        {
            return Md5Util.GetStringMd5($"{name},{size}");
        }
    }

    public class  FileSegment
    {
        public string hash { get; set; }
        public string block { get; set; }
        public int length { get; set; }
        public string data { get; set; }
    }
}

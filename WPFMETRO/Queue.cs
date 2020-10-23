﻿using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace WPFMETRO
{
    public class QueueException : Exception
    {
        public QueueException(string message)
           : base(message)
        {
        }
    }
    class Queue
    {
        public ObservableCollection<Video> Videos = new ObservableCollection<Video>();

        YouTubeService yt = new YouTubeService(new BaseClientService.Initializer() { ApiKey = "" });
        private Process ytbDL = new Process();

        private ProcessStartInfo ytbDLInfo = new ProcessStartInfo
        {

            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding(65001),
            FileName = "youtube-dl.exe"
        };

        public List<string> GetInfo(string url)
        {
            string[] args = { "--encoding utf-8 --skip-download --get-title --no-warnings " + url, "--encoding utf-8 --skip-download --list-thumbnails --no-warnings " + url };
            List<string> response = new List<string>();
            bool isThumb = false;
            bool isNext = false;

            foreach (string argument in args)
            {
                ytbDLInfo.Arguments = argument;
                ytbDL = Process.Start(ytbDLInfo);
                string info = null;

                ytbDL.OutputDataReceived += new DataReceivedEventHandler(
                    (s, f) =>
                    {
                        string output = f.Data ?? "null";
                        if (isThumb)
                        {
                            if(isNext && info == null)
                            {
                                info = output == "null" || output.Contains("No thumbnail") ? info : output.Substring(output.LastIndexOf(' ')+1);
                            }
                            else
                            {
                                isNext = output == null ? false : output.Contains("width");
                            }
                        }
                        else
                        {
                            info = output == "null" ? info : output;
                        }
                    });

                ytbDL.Start();
                ytbDL.BeginOutputReadLine();
                ytbDL.WaitForExit();
                ytbDL.CancelOutputRead();

                response.Add(info);
                isThumb = true;
            }

            return response;
        }

        public Dictionary<string, string> GetFormats(string url)
        {
            Dictionary<string, string> formats = new Dictionary<string, string>();

            if (!url.Contains("youtube.com/playlist?list"))
            {
                string output = "";
                string arguments = "-F " + url;
                bool isNext = false;
                bool hasAudio = false;

                ytbDLInfo.Arguments = arguments;

                ytbDL = Process.Start(ytbDLInfo);

                ytbDL.OutputDataReceived += new DataReceivedEventHandler(
                    (s, f) =>
                    {
                        output = f.Data ?? "null";

                        if (output != "null")
                        {
                            if (isNext)
                            {
                                string code, desc;

                                MatchCollection values = Regex.Matches(output, @"([^\s]+)");
                                if (values.Count > 4)
                                {
                                    desc = values[1].ToString() + " (";
                                    if (values[2].ToString() == "audio")
                                    {
                                        code = values[0].ToString();
                                        desc += "audio";
                                        desc += values.Count > 5 && values[5].ToString() != "audio" ? " " + values[5].ToString() + ")" : ")";
                                    }
                                    else
                                    {
                                        code = values[0].ToString() + ' ' + values[1].ToString();
                                        desc += values[2].ToString() + ' ' + values[3].ToString() + ')';
                                    }
                                }
                                else
                                {
                                    code = values[0].ToString();
                                    desc = values[1].ToString() + " (" + values[2].ToString() + ")";
                                }
                                hasAudio = !hasAudio && values[1].ToString() == "mp4" || values[1].ToString() == "webm" || values[1].ToString() == "m4a" ? true : hasAudio;
                                code += output.Contains("video only") ? "-y" : "-n";

                                formats.Add(code, desc);
                            }
                            else if (output.IndexOf(" ") > 0) { isNext = output.Substring(0, output.IndexOf(" ")) == "format"; }
                        }
                    });

                ytbDL.Start();
                ytbDL.BeginOutputReadLine();
                ytbDL.WaitForExit();
                ytbDL.CancelOutputRead();

                if(formats.Count > 1)
                {
                    formats.Add("default", "default");
                    if (formats.Count > 1 && hasAudio)
                    {
                        formats.Add("mp3", "mp3");
                        formats.Add("flac", "flac");
                    }
                }
                
            }

            return formats;
        }


        public void DownloadPlaylist(string ID, string filename, string path, string filetype, Dictionary<string, string> formats)
        {
            var nextPageToken = "";
            while (nextPageToken != null)
            {
                var playlistItemsListRequest = yt.PlaylistItems.List("snippet");
                playlistItemsListRequest.PlaylistId = ID.Substring(ID.IndexOf("=") + 1);
                playlistItemsListRequest.MaxResults = 50;
                playlistItemsListRequest.PageToken = nextPageToken;

                var playlistItemsListResponse = playlistItemsListRequest.Execute();

                foreach (var playlistItem in playlistItemsListResponse.Items)
                {
                    var videoRequest = yt.Videos.List("snippet,status");

                    videoRequest.Id = playlistItem.Id;

                    var videoListResponse = videoRequest.Execute();

                    if (videoListResponse.Items.Count < 1)
                    {
                        throw new QueueException(Localization.Strings.InvalidURL);
                    }
                    foreach (var videoItem in videoListResponse.Items)
                    {
                        if (videoItem.Status != null)
                        {
                            ModifyQueue(videoItem.Snippet.Title, videoItem.Snippet.Thumbnails.Default__.Url, "https://www.youtube.com/watch?v=" + playlistItem.Snippet.ResourceId.VideoId, filename, path, filetype, formats);
                        }
                    }
                }

                nextPageToken = playlistItemsListResponse.NextPageToken;
            }
        }

        public void DownloadChannel(string ID, string filename, string path, string filetype, Dictionary<string, string> formats)
        {
            var channelItemsRequest = yt.Channels.List("contentDetails");
            channelItemsRequest.Id = ID.Substring(ID.IndexOf("channel/") + 8);
            channelItemsRequest.MaxResults = 50;

            var channelsListResponse = channelItemsRequest.Execute();

            foreach (var item in channelsListResponse.Items)
            {
                string uploadsListID = item.ContentDetails.RelatedPlaylists.Uploads;

                DownloadPlaylist(uploadsListID, filename, path, filetype, formats);
            }


        }

        public void ModifyQueue(string title, string thumbURL, string ID, string filename, string path, string filetype, Dictionary<string, string> formats)
        {
            if(filetype == null)
            {
                MessageBox.Show(Localization.Strings.PleaseSelectFormat);
                return;
            }

            string fullpath = filename == "%(title)s.%(ext)s" ? path + title + "." + formats[filetype] : path + filename + "." + formats[filetype];
            if (File.Exists(fullpath))
            {
                MessageBoxResult userDialogResult = MessageBox.Show(Localization.Strings.FileExists, Localization.Strings.Error, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if(userDialogResult == MessageBoxResult.Yes)
                {
                    File.Delete(fullpath);
                }
                else
                {
                    return;
                }
            }
            if (ID.Contains("youtu.be/") || ID.Contains("youtube.com/"))
            {
                if (ID.Contains("playlist"))
                {
                    DownloadPlaylist(ID, filename, path, filetype, formats);
                }
                else if (ID.Contains("channel"))
                {
                    DownloadChannel(ID, filename, path, filetype, formats);
                }
                else
                {
                        Video video = new Video();
                        video.ID = ID;
                        video.Name = filename;
                        video.Path = path;
                        video.SelectedFormat = filetype;
                        video.ThumbURL = thumbURL;
                        video.Title = title;
                        video.AvailableFormats = formats.ToList();

                        Videos.Add(video);
                }
            }
            else
            {
                switch (ID.Length)
                {
                    case 0:
                        throw new QueueException(Localization.Strings.InvalidURL);
                    default:
                        Video videoNonYoutube = new Video();
                        videoNonYoutube.ID = ID;
                        videoNonYoutube.Name = filename;
                        videoNonYoutube.Path = path;
                        videoNonYoutube.SelectedFormat = filetype;
                        videoNonYoutube.ThumbURL = thumbURL;
                        videoNonYoutube.Title = title;
                        videoNonYoutube.AvailableFormats = formats.ToList();

                        Videos.Add(videoNonYoutube);
                        break;
                }
            }
        }
    }
}

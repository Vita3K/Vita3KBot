#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using Discord;
using Newtonsoft.Json;

using PSN.POCOs;

namespace Vita3KBot.APIClients.PSNClient
{
  public static class PSNClient
  {
    private static readonly byte[] HMACKey = [
      0xE5, 0xE2, 0x78, 0xAA, 0x1E, 0xE3, 0x40, 0x82, 0xA0, 0x88, 0x27, 0x9C, 0x83, 0xF9, 0xBB, 0xC8,
      0x06, 0x82, 0x1C, 0x52, 0xF2, 0xAB, 0x5D, 0x2B, 0x4A, 0xBD, 0x99, 0x54, 0x50, 0x35, 0x51, 0x14
    ];

    private static readonly HMACSHA256 HMAC = new(HMACKey);
    private static readonly string BaseURL = "http://gs-sec.ww.np.dl.playstation.net/pl/np/";

    // all firmware regions are the same therefore use US as default
    private static readonly string FirmwareXML = "http://fus01.psp2.update.playstation.net/update/psp2/list/us/psp2-updatelist.xml";
    private static readonly XmlSerializer FWSerializer = new(typeof(UpdateDataList));
    private static readonly XmlSerializer PatchesSerializer = new(typeof(TitlePatch));

    public static (Embed, MessageComponent?) GetTitlePatch(string titleId)
    {
      string url = ConvertTitleIDToHash(titleId);
      var covers = JsonConvert.DeserializeObject<Root>(File.ReadAllText("./APIClients/PSNClient/Covers.json"));

      XmlDocument xmlDoc = new();
      try
      {
        xmlDoc.Load(url);
      }
      catch (HttpRequestException e)
      {
        if (e.StatusCode == HttpStatusCode.NotFound)
        {
          return BuildNoUpdatesResult(titleId, covers);
        }
      }
      catch (WebException)
      {
        return BuildNoUpdatesResult(titleId, covers);
      }
      catch (XmlException)
      {
        return BuildNoUpdatesResult(titleId, covers);
      }

      using XmlReader reader = XmlReader.Create(url);
      TitlePatch patch = (TitlePatch)PatchesSerializer.Deserialize(reader);
      return (BuildPatchEmbed(titleId, patch), null);
    }

    private static (Embed, MessageComponent?) BuildNoUpdatesResult(string titleId, Root? covers)
    {
      // If it doesn't start with PCS, search for candidates by name.
      if (!titleId.StartsWith("PCS", StringComparison.OrdinalIgnoreCase))
      {
        var matched = covers?.IDs?
          .Where(x => x.name != null && x.name.Contains(titleId, StringComparison.OrdinalIgnoreCase))
          .ToList();

        if (matched != null && matched.Count == 1)
        {
          return GetTitlePatch(matched[0].ID);
        }

        if (matched != null && matched.Count > 1)
        {
          return BuildMultipleCandidatesResult(matched);
        }
      }

      return (new EmbedBuilder
      {
        Title = titleId,
        Description = $"No updates were found for {titleId}",
        Color = Color.Orange
      }.Build(), null);
    }

    private static (Embed, MessageComponent?) BuildMultipleCandidatesResult(List<IDs> matched)
    {
      var embed = new EmbedBuilder
      {
        Title = "Which Title ID？",
        Description = string.Join("\n", matched.Select(x => $"`{x.ID}` - {x.name}")),
        Color = Color.Orange
      }.Build();

      var options = matched.Select(m => new SelectMenuOptionBuilder()
        .WithLabel(m.ID)
        .WithValue(m.ID)
        .WithDescription(m.name)  // Game name shown as sub-text in the menu
        .WithEmote(new Emoji(GetRegionFlag(m.ID)))
      ).ToList();

      var menu = new SelectMenuBuilder()
        .WithCustomId("update_select")  // Must match the handler's CustomId
        .WithPlaceholder("Select a Title ID")
        .WithOptions(options)
        .WithMinValues(1)
        .WithMaxValues(1);

      var components = new ComponentBuilder()
        .WithSelectMenu(menu)
        .Build();

      return (embed, components);
    }

    private static string GetRegionFlag(string id) => id switch
    {
      _ when id.StartsWith("PCSA", StringComparison.OrdinalIgnoreCase) => "🇺🇸",
      _ when id.StartsWith("PCSB", StringComparison.OrdinalIgnoreCase) => "🇪🇺",
      _ when id.StartsWith("PCSC", StringComparison.OrdinalIgnoreCase) => "🇯🇵",
      _ when id.StartsWith("PCSD", StringComparison.OrdinalIgnoreCase) => "🇨🇳",
      _ when id.StartsWith("PCSE", StringComparison.OrdinalIgnoreCase) => "🇺🇸",
      _ when id.StartsWith("PCSF", StringComparison.OrdinalIgnoreCase) => "🇪🇺",
      _ when id.StartsWith("PCSG", StringComparison.OrdinalIgnoreCase) => "🇯🇵",
      _ when id.StartsWith("PCSH", StringComparison.OrdinalIgnoreCase) => "🇨🇳",
      _ when id.StartsWith("PCSI", StringComparison.OrdinalIgnoreCase) => "🌍",
      _ => "🎮",
    };

    private static Embed BuildPatchEmbed(string titleId, TitlePatch patch)
    {
      var pkgs = patch.Tag.Package;
      var title = pkgs.Select(p => p.Sfo?.Title).LastOrDefault(t => !string.IsNullOrEmpty(t));
      var Covers = JsonConvert.DeserializeObject<Root>(File.ReadAllText("./APIClients/PSNClient/Covers.json")); // Relative to Bot.cs

      string coverURL = FindCoverUrl(Covers, titleId);

      var patchEmbed = new EmbedBuilder
      {
        Title = title,
        Color = Color.Orange,
      };
      patchEmbed.WithFooter(f => f.Text = $"Content ID: {patch.Tag.Package[0].ContentId}");
      if (coverURL != null)
      {
        patchEmbed.ThumbnailUrl = coverURL;
      }

      // Credit to RPCS3-Bot (13xforever) for this code https://github.com/RPCS3/discord-bot - https://github.com/13xforever
      if (pkgs.Length > 1)
      {
        AddMultiPackageFields(patchEmbed, pkgs);
      }
      else if (pkgs.Length == 1)
      {
        AddSinglePackageFields(patchEmbed, pkgs[0]);
      }

      return patchEmbed.Build();
    }

    private static string FindCoverUrl(Root covers, string titleId)
    {
      for (int i = 0; i < covers.IDs.Length; i++)
      {
        if (covers.IDs[i].ID == titleId)
        {
          return covers.IDs[i].cover;
        }
      }

      return string.Empty;
    }

    private static void AddMultiPackageFields(EmbedBuilder patchEmbed, Package[] pkgs)
    {
      var i = 0;
      do
      {
        var pkg = pkgs[i++];
        patchEmbed.AddField($"Update v{pkg.Version} - ({ToMB(pkg.Size)}MB) - Min Firmware: {FormatSysVer(pkg.SysVer)}", $"[{pkg.Url.Substring(103, 28)}.pkg]({pkg.Url})");
      } while (i < pkgs.Length);

      patchEmbed.AddField($"Hybrid Package ({ToMB(pkgs[^1].HybridPackage.Size)}MB) - " +
        $"Min Firmware: {FormatSysVer(pkgs[^1].SysVer)}", $"[{pkgs[^1].Url.Substring(103, 28)}.pkg]({pkgs[^1].HybridPackage.Url})");
      patchEmbed.Description = $"Content ID: {pkgs[0].ContentId}";
      patchEmbed.WithFooter(f => f.Text = $"Note: Hybrid Packages contain all previous updates");
    }

    private static void AddSinglePackageFields(EmbedBuilder patchEmbed, Package pkg)
    {
      patchEmbed.Title = $"{pkg.Sfo.Title} v{pkg.Version} ({ToMB(pkg.Size)}MB)";
      patchEmbed.Description = $"[{pkg.Url.Substring(103, 28)}.pkg]({pkg.Url})";
      patchEmbed.AddField("Min Firmware", $"{FormatSysVer(pkg.SysVer)}");
    }

    public static string GetFWVersion()
    {
      using XmlReader reader = XmlReader.Create(FirmwareXML);
      UpdateDataList updateDataList = (UpdateDataList)FWSerializer.Deserialize(reader);
      return updateDataList.Region.Version.Label.ToString();
    }

    public static (string, double) GetFullFW()
    {
      using XmlReader reader = XmlReader.Create(FirmwareXML);
      UpdateDataList updateDataList = (UpdateDataList)FWSerializer.Deserialize(reader);
      return (updateDataList.Region.Version.UpdateData.Image.Text, ToMB(updateDataList.Region.Version.UpdateData.Image.Size));
    }

    public static (string, double) GetSystemDataFW()
    {
      using XmlReader reader = XmlReader.Create(FirmwareXML);
      UpdateDataList updateDataList = (UpdateDataList)FWSerializer.Deserialize(reader);
      return (updateDataList.Region.Recovery[0].Image.Text, ToMB(updateDataList.Region.Recovery[0].Image.Size));
    }

    public static (string, double) GetPreinstDataFW()
    {
      using XmlReader reader = XmlReader.Create(FirmwareXML);
      UpdateDataList updateDataList = (UpdateDataList)FWSerializer.Deserialize(reader);
      return (updateDataList.Region.Recovery[1].Image.Text, ToMB(updateDataList.Region.Recovery[1].Image.Size));
    }

    // Credit to VitaSmith for this code https://github.com/VitaSmith
    private static string ConvertTitleIDToHash(string titleId)
    {
      // Getting the title id and giving the link back
      byte[] hash = HMAC.ComputeHash(new ASCIIEncoding().GetBytes("np_" + titleId));
      string patchUrl = BaseURL + titleId + "/" + BitConverter.ToString(hash).ToLower().Replace("-", "") + "/" + titleId + "-ver.xml";
      return patchUrl;
    }

    private static double ToMB(double size)
    {
      size = size / 1024 / 1024;
      size = Math.Round(size, 2);
      return size;
    }

    // Credit to VitaSmith for this code https://github.com/VitaSmith
    private static string FormatSysVer(uint sysver)
    {
      sysver /= 0x10000;
      sysver = (sysver / 0x1000 * 1000) + ((sysver & 0x0F00) / 0x100 * 100) + ((sysver & 0x00F0) / 0x10 * 10) + (sysver & 0x000F);

      return sysver.ToString().Insert(0, "v").Insert(2, ".");
    }
  }
}

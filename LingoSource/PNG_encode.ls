 -- ************************************************************************
-- Software: PNG Encoder
-- Version:  1.3 - d10 (string)
-- Date:     2009-03-30
-- Author:   Valentin Schmidt
-- Contact:  fluxus@freenet.de
-- License:  Freeware
--
-- Requirements/Dependencies:
-- * crc32-checksum (e.g. Crypto Xtra or lingo function)
-- * zlib-compression (e.g. zlibXtra or shell xtra + zip)
-- * a file write xtra (e.g. fileIO xtra, BinFile Xtra or Crypto Xtra)
--
-- Last Changes:
-- * added support for transparency (for images without alpha channel)
--   transparency is specified as
--   a) a colorObject for 24 Bit RGB images
--   b) for palette images either as a number (1-256) (which means: make color n in palette tramsparent, and all 
--      other colors opaque) or a complete list of transparency values (0..255) which specify the 
--      transparency value (0=transparent, 255=opaque) for each color in the palette
--   c) a number (1-256) for grayscale images
--
-- ************************************************************************

property pData

property pCrcTable
property pUseXtra

----------------------------------------
-- 
----------------------------------------
on PNG_encode (me, tImage, tCompression, tTranparency)
  if voidP(tCompression) then tCompression=6
  
  pUseXtra = xtraPresent("Crypto")
  if not pUseXtra then
    pCrcTable = [0, 1996959894, -301047508, -1727442502, 124634137, 1886057615, -379345611, -1637575261, 249268274, 2044508324, -522852066, -1747789432, 162941995, 2125561021, -407360249, -1866523247, 498536548, 1789927666, -205950648, -2067906082, 450548861, 1843258603, -187386543, -2083289657, 325883990, 1684777152, -43845254, -1973040660, 335633487, 1661365465, -99664541, -1928851979, 997073096, 1281953886, -715111964, -1570279054, 1006888145, 1258607687, -770865667, -1526024853, 901097722, 1119000684, -608450090, -1396901568, 853044451, 1172266101, -589951537, -1412350631, 651767980, 1373503546, -925412992, -1076862698, 565507253, 1454621731, -809855591, -1195530993, 671266974, 1594198024, -972236366, -1324619484, 795835527, 1483230225, -1050600021, -1234817731, 1994146192, 31158534, -1731059524, -271249366, 1907459465, 112637215, -1614814043, -390540237, 2013776290,   251722036, -1777751922, -519137256, 2137656763, 141376813, -1855689577, -429695999, 1802195444, 476864866, -2056965928, -228458418, 1812370925, 453092731, -2113342271, -183516073, 1706088902, 314042704, -1950435094, -54949764, 1658658271, 366619977, -1932296973, -69972891, 1303535960, 984961486, -1547960204, -725929758, 1256170817, 1037604311, -1529756563, -740887301, 1131014506, 879679996, -1385723834, -631195440, 1141124467, 855842277, -1442165665, -586318647, 1342533948, 654459306, -1106571248, -921952122, 1466479909, 544179635, -1184443383, -832445281, 1591671054, 702138776, -1328506846, -942167884, 1504918807, 783551873, -1212326853, -1061524307, -306674912, -1698712650, 62317068, 1957810842, -355121351, -1647151185, 81470997, 1943803523, -480048366, -1805370492, 225274430, 2053790376, -468791541, -1828061283, 167816743, 2097651377, -267414716, -2029476910, 503444072, 1762050814, -144550051, -2140837941, 426522225, 1852507879, -19653770, -1982649376, 282753626, 1742555852, -105259153, -1900089351, 397917763, 1622183637, -690576408, -1580100738, 953729732, 1340076626, -776247311, -1497606297, 1068828381, 1219638859, -670225446, -1358292148, 906185462, 1090812512, -547295293, -1469587627, 829329135, 1181335161, -882789492, -1134132454, 628085408, 1382605366, -871598187, -1156888829, 570562233, 1426400815, -977650754, -1296233688, 733239954, 1555261956, -1026031705, -1244606671, 752459403, 1541320221, -1687895376, -328994266, 1969922972, 40735498, -1677130071, -351390145, 1913087877, 83908371, -1782625662, -491226604, 2075208622, 213261112, -1831694693, -438977011, 2094854071, 198958881, -2032938284, -237706686, 1759359992, 534414190, -2118248755, -155638181, 1873836001, 414664567, -2012718362, -15766928, 1711684554, 285281116, -1889165569, -127750551, 1634467795, 376229701, -1609899400, -686959890, 1308918612, 956543938, -1486412191, -799009033, 1231636301, 1047427035, -1362007478, -640263460, 1088359270, 936918000, -1447252397, -558129467, 1202900863, 817233897, -1111625188, -893730166, 1404277552, 615818150, -1160759803, -841546093, 1423857449, 601450431, -1285129682, -1000256840, 1567103746, 711928724, -1274298825, -1022587231, 1510334235, 755167117]
  end if
  
  if the platform contains "win" then
    tMaxStrLen = 20000
  else
    tMaxStrLen = 5000 -- ???
  end if
  
  -- Write PNG signature
  pData = numtochar(137)&"PNG"
  me.writeBytes(13,10,26,10) 
  
  --  PNG color types:
  --  0: GS
  --  2: RGB
  --  3: Palette, 8 bit
  --  4: GS with alpha
  --  6: RGB with alpha
  
  case (tImage.depth) of
    8:
      if tImage.paletteRef=#grayscale then
        tColorType=0 -- GS
      else
        tColorType=3 -- Palette
      end if
      
    24, 32:
      if tImage.useAlpha then
        tColorType=6 -- RGB with alpha
        tAlpha = tImage.extractAlpha()
      else
        tColorType=2 -- RGB
      end  if
      
    otherwise:
      return false -- 1, 4, 16-bit unsupported!
      
  end case
  
  -- Build IHDR chunk
  IHDR = ""
  put writeInt(tImage.width) after IHDR
  put writeInt(tImage.height) after IHDR
  --put writeBytes(8,tColorType,0,0,0) after IHDR -- 24bit RGB | 32bit RGBA
  put numtochar(8) after IHDR
  put numtochar(tColorType) after IHDR
  put numtochar(0) after IHDR
  put numtochar(0) after IHDR
  put numtochar(0) after IHDR
  
  me.writeChunk("IHDR", IHDR)
  
  -- Build IDAT chunk
  IDAT = ""
  dat = []
  tmp = ""
  
  case (tColorType) of
    0: -- GS
      repeat with y=0 to tImage.height-1
        put numtochar(0) after tmp -- no filter
        repeat with x=0 to tImage.width-1          
          
          p = tImage.getPixel(x,y)
          put numtochar(255-p.paletteIndex) after tmp
          
        end repeat
        if tmp.length>tMaxStrLen then
          dat.add(tmp)
          tmp=""
        end if
      end repeat
      
    2: -- RGB
      repeat with y=0 to tImage.height-1
        put numtochar(0) after tmp -- no filter
        repeat with x=0 to tImage.width-1
          p = tImage.getPixel(x,y)
          put numtochar(p.red)&numtochar(p.green)&numtochar(p.blue) after tmp
          
        end repeat
        if tmp.length>tMaxStrLen then
          dat.add(tmp)
          tmp=""
        end if
      end repeat
      
    3: -- Palette
      
      -- extract palette from image data itself
      tTmpImg = image(tImage.width, tImage.height, 24)
      tTmpImg.copyPixels(tImage, tTmpImg.rect, tTmpImg.rect)
      pal = [] 
      repeat with y=0 to tImage.height-1
        put numtochar(0) after tmp -- no filter
        repeat with x=0 to tImage.width-1
          
          col = tTmpImg.getPixel(x,y)
          pos = pal.getPos(col)
          if pal.count<256 then
            if pos=0 then 
              pal.add(col)
              pos = pal.count
            end if
          end if
          put numtochar(pos-1) after tmp
          
        end repeat
        if tmp.length>tMaxStrLen then
          dat.add(tmp)
          tmp=""
        end if
      end repeat
      tTmpImg = VOID
      
    6: -- RGB with alpha
      repeat with y=0 to tImage.height-1
        put numtochar(0) after tmp -- no filter
        repeat with x=0 to tImage.width-1
          p = tImage.getPixel(x,y)
          a = tAlpha.getPixel(x,y).paletteIndex
          put numtochar(p.red)&numtochar(p.green)&numtochar(p.blue)&numtochar(a) after tmp
        end repeat
        if tmp.length>tMaxStrLen then
          dat.add(tmp)
          tmp=""
        end if
      end repeat
      
  end case
  
  repeat with str in dat
    put str after IDAT
  end repeat
  put tmp after IDAT
  
  -- custom palette
  if tColorType=3 then
    
    PLTE = ""
    repeat with col in pal
      put numtochar(col.red)&numtochar(col.green)&numtochar(col.blue) after PLTE
    end repeat
    repeat with i = pal.count+1 to 256
      put numtochar(0)&numtochar(0)&numtochar(0) after PLTE
    end repeat
    me.writeChunk("PLTE", PLTE)
    
  end if
  
  -- tRNS
  if not voidP(tTranparency) then
    case (tColorType) of
      0: -- GS
        tRNS  = ""
        put numtochar(0) & numtochar(tTranparency) after tRNS
        me.writeChunk("tRNS", tRNS)
        
      2: -- RGB
        tRNS  = ""
        put numtochar(0) & numtochar(tTranparency.red) after tRNS
        put numtochar(0) & numtochar(tTranparency.green) after tRNS
        put numtochar(0) & numtochar(tTranparency.blue) after tRNS
        me.writeChunk("tRNS", tRNS)
        
      3: -- Palette
        tRNS  = ""
        if integerP(tTranparency) then
          repeat with i = 1 to 256
            if i=tTranparency then
              put numtochar(0) after tRNS
            else
              put numtochar(255) after tRNS
            end if
          end repeat
        else if listP(tTranparency) then
          repeat with t in tTranparency
            put numtochar(t) after tRNS
          end repeat
          repeat with i = tTranparency.count+1 to 256
            put numtochar(255) after tRNS
          end repeat
        end if
        
        me.writeChunk("tRNS", tRNS)
    end case
    
  end if
  
  -- compress
  IDAT = gzcompress(IDAT, tCompression)  
  me.writeChunk("IDAT", IDAT)
  
  -- Build IEND chunk
  me.writeChunk("IEND", VOID)
  me.writeBytes(174,66,96,130) -- CRC for IEND: AE 42 60 82
  
  return pData
end

----------------------------------------
-- 
----------------------------------------
on writeChunk (me, type, data)
  if voidP(data) then data = ""
  len = data.length 
  put writeInt(len) after pData
  put type after pData
  put data after pData
  if data<>"" then
    put type before data
    --put crc32(data) after pData
    me.writeCRC(data)
  end if
end

----------------------------------------
-- 
----------------------------------------
on writeBytes (me)
  cnt = paramCount()
  repeat with i = 2 to cnt
    put numtochar(param(i)) after pData
  end repeat
end

----------------------------------------
-- 
----------------------------------------
on writeInt (n)
  s=""
  put numtochar(bitAnd(n, 4278190080)/16777216) after s
  put numtochar(bitAnd(n, 16711680)/65536) after s
  put numtochar(bitAnd(n, 65280)/256) after s
  put numtochar(bitAnd(n, 255)) after s
  return s
end

----------------------------------------
-- uses zlibXtra
-- adjust for alternative zlib compression method
----------------------------------------
on gzcompress (str, comp)
  return zx_gzcompress_string(str, comp)
end

----------------------------------------
-- adjust for alternative CRC32 calculation method
----------------------------------------
on writeCRC (me, data)
  if pUseXtra then
    put cx_crc32_string(data, 1) after pData -- CRYPTO-XTRA
  else
    put writeInt(me.lingo_crc32(data)) after pData -- LINGO
  end if
end

----------------------------------------
-- 
----------------------------------------
on lingo_crc32(me, str)
  crc = -1
  len = str.length
  repeat with i = 1 to len 
    crc = bitXor(bitShift8(crc),pCrcTable[bitAnd(bitXor(crc,chartonum(str.char[i])),255)+1])    
  end repeat
  return bitXOr(crc,-1)
end

----------------------------------------
-- 
----------------------------------------
on bitShift8(n)
  if (n>0) then return n/256
  else return bitAnd(n,2147483647)/256+8388608
end

----------------------------------------
--
----------------------------------------
on xtraPresent (xtraName)
  n=the number of xtras
  repeat with i = 1 to n
    if xtra(i).name = xtraName then return 1
  end repeat
  return 0
end



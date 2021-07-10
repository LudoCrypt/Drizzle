global gLEVEL, gSEPropson exitFrame me  gSEProps = [#sounds:[], #ambientSounds:[], #songs:[], #rects:[], #pickedUpSound:"NONE"]  repeat with q = 1 to 4 then    gSEProps.sounds.add([#mem:"None", #vol:0, #pan:0])  end repeat    repeat with q = 1 to gLEVEL.ambientSounds.count then    gSEProps.sounds[q] = gLEVEL.ambientSounds[q]  end repeat    repeat with q = 1 to 4 then    sprite(38+q).loc = point(100, (q-1)*150+100)    sprite(38+q).visibility = 1        repeat with c = 1 to 2 then      -- put 21+((q-1)*2)+c      sprite(21+((q-1)*2)+c).rect = rect(100, (q-1)*150 + c*50 + 100, 600, (q-1)*150 + c*50 + 102)      --put rect(100, (q-1)*100 + c*50 + 200, 600, (q-1)*100 + c*50 + 202)      --  sprite(21+((q-1)*2)+c).rect = rect(100, 100, 200, 200)      gSEProps.rects.add([rect(100, (q-1)*150 + c*50 + 100, 600, (q-1)*150 + c*50 + 100)+rect(-50, -20, 50, 20), [q,c]])    end repeat    if gSEProps.sounds[q].mem = "None" then      member("AmbienceSound"&q).text="No Ambience Sound"    else      member("AmbienceSound"&q).text=gSEProps.sounds[q].mem      sav = member("amb"&q)      member("amb"&q).importFileInto("music\ambience\" & gSEProps.sounds[q].mem &".mp3")      sav.name = "amb"&q      sound(q).pan = gSEProps.sounds[q].pan      sound(q).volume = gSEProps.sounds[q].vol      sound(q).play([#member:sav, #loopCount:0])    end if  end repeat      fileList = [ ]  repeat with i = 1 to 100 then    n = getNthFileNameInFolder(the moviePath & "\music\ambience", i)    if n = EMPTY then exit repeat    fileList.append(n)  end repeat  projects = ["QUIET"]  repeat with l in fileList then    projects.add( chars(l, 1, l.length-4))  end repeat  txt = "Ambient Sounds:"  put RETURN after txt  put RETURN after txt  repeat with q in projects then    gSEProps.ambientSounds.add(q)    put q after txt    put RETURN after txt  end repeat    put RETURN after txt  put RETURN after txt    fileList = [ ]  repeat with i = 1 to 100 then    n = getNthFileNameInFolder(the moviePath & "\music", i)    if n = EMPTY then exit repeat    fileList.append(n)  end repeat  projects = ["NONE"]  repeat with l in fileList then    projects.add( chars(l, 1, l.length-4))  end repeat  put RETURN after txt  put "Songs:" after txt  put RETURN after txt  put RETURN after txt  repeat with q in projects then    if (q <> "ambi")and(q <> "overwrite") then      gSEProps.songs.add(q)      put q after txt      put RETURN after txt    end if  end repeat      member("AMBsoundL").text = txt    member("buttonText").text = ""    repeat with q in gSEprops.songs then    if q = gLEVEL.music then      sprite(12).loc = point(750, 6+(gSEprops.songs.getPos(q)+4+gSEprops.ambientSounds.count+5)*12)    end if  end repeat    if gLEVEL.music = "none" then    sav = member("music")    member("music").importFileInto("music\" & "overwrite" &".mp3")    sav.name = "music"    sound(5).pan = 0    sound(5).volume = 0    sound(5).stop()  else    sav = member("music")    member("music").importFileInto("music\" & gLEVEL.music &".mp3")    sav.name = "music"    sound(5).pan = 0    sound(5).volume = 255    sound(5).play([#member:sav, #loopCount:0])  end if  end
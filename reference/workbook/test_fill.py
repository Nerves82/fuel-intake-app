import datetime as dt, openpyxl, sys
wb = openpyxl.load_workbook("Fuel_Assistance_Intake.xlsx")
def setlab(sheet, label, val):
    ws = wb[sheet]
    for row in ws.iter_rows(min_col=2, max_col=2):
        if row[0].value == label:
            ws.cell(row=row[0].row, column=3).value = val; return
    raise SystemExit(f"label not found: {sheet} / {label}")
setlab("1 Client", "Application date", dt.datetime(2026, 11, 16))
setlab("1 Client", "Staff name", "Steve B.")
setlab("1 Client", "Client first name", "Jane")
setlab("1 Client", "Client last name", "O'Brien & Co <test>")
setlab("1 Client", "Client date of birth", dt.datetime(1961, 3, 14))
setlab("1 Client", "Physical address", "123 Main St"); setlab("1 Client", "Phone number", "0802555012")
setlab("1 Client", "City/Town", "St. Johnsbury"); setlab("1 Client", "State", "VT"); setlab("1 Client", "Zip", "05819")
setlab("1 Client", "Housing type", "Apartment"); setlab("1 Client", "County", "Caledonia")
setlab("1 Client", "Household type", "Single Parent Female")
setlab("1 Client", "SNAP", "Yes")
setlab("1 Client", "SNAP amount", 291); setlab("1 Client", "SNAP amount per", "month"); setlab("1 Client", "SNAP verified in ACCESS (date)", dt.datetime(2026,11,16))
hh = wb["2 Household"]
# row 6 = member 1, row 7 = member 2
hh["H6"], hh["M6"], hh["N6"], hh["P6"], hh["Q6"] = "N", 1450, "SSA", "008705217", "Retired"
hh["C7"], hh["D7"], hh["E7"], hh["H7"], hh["M7"], hh["N7"], hh["P7"], hh["Q7"] = "O'Brien", "Kid", dt.datetime(2016,5,1), "N", 0, "", "012-34-5678", "N/A"
hh["C8"], hh["D8"], hh["E8"], hh["H8"], hh["M8"], hh["N8"], hh["Q8"] = "Doe", "Sam", dt.datetime(2008,1,1), "Y", 300, "Earned", "In school full-time and employed part-time"
setlab("3 Application", "Crisis type", "Low on Fuel")
setlab("3 Application", "Seasonal Fuel status", "Pending")
setlab("3 Application", "Unforeseen / extenuating circumstance — 3102(c)6", 'Medical - "recent" relocation\nsecond line')
ie = wb["4 Income & Expenses"]
ie["C10"] = 1450   # SSA
ie["C8"] = 300     # earned
ie["F19"] = 500; ie["G19"] = 600
setlab("5 Fuel & Electric", "Fuel $", 400); setlab("5 Fuel & Electric", "Time", dt.time(14, 30))
setlab("5 Fuel & Electric", "Start Up $", 50)
wb.save("test.xlsx")

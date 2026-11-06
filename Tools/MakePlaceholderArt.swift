#!/usr/bin/env swift
// Generates labelled placeholder PNG art for DevilGate.
// Usage: swift Tools/MakePlaceholderArt.swift [portraits|items|world|all]
import AppKit

struct ArchetypeArt {
    let id: String
    let name: String
    let species: String
    let color: NSColor
}

let archetypes: [ArchetypeArt] = [
    ArchetypeArt(id: "mage",    name: "MAGE",    species: "DARK ELF",  color: NSColor(calibratedRed: 0.54, green: 0.36, blue: 0.96, alpha: 1)),
    ArchetypeArt(id: "tank",    name: "TANK",    species: "ORC",       color: NSColor(calibratedRed: 0.44, green: 0.62, blue: 0.29, alpha: 1)),
    ArchetypeArt(id: "archer",  name: "ARCHER",  species: "ELF",       color: NSColor(calibratedRed: 0.30, green: 0.76, blue: 0.54, alpha: 1)),
    ArchetypeArt(id: "knight",  name: "KNIGHT",  species: "HUMAN",     color: NSColor(calibratedRed: 0.44, green: 0.56, blue: 0.77, alpha: 1)),
    ArchetypeArt(id: "healer",  name: "HEALER",  species: "HIGH HUMAN",color: NSColor(calibratedRed: 0.91, green: 0.76, blue: 0.35, alpha: 1)),
]

let outDir = URL(fileURLWithPath: CommandLine.arguments.count > 2 ? CommandLine.arguments[2] : "Assets/Placeholders")

func savePNG(_ image: NSImage, to url: URL) {
    guard let tiff = image.tiffRepresentation,
          let rep = NSBitmapImageRep(data: tiff),
          let png = rep.representation(using: .png, properties: [:]) else {
        print("FAILED to encode \(url.path)")
        return
    }
    try? png.write(to: url)
    print("wrote \(url.path)")
}

func text(_ s: String, font: NSFont, color: NSColor, centerX: CGFloat, y: CGFloat) {
    let attrs: [NSAttributedString.Key: Any] = [
        .font: font, .foregroundColor: color,
    ]
    let str = NSAttributedString(string: s, attributes: attrs)
    let size = str.size()
    str.draw(at: NSPoint(x: centerX - size.width / 2, y: y))
}

func makePortrait(_ arch: ArchetypeArt, gender: String) -> NSImage {
    let size = NSSize(width: 512, height: 768)
    let image = NSImage(size: size)
    image.lockFocus()

    // Background: archetype colour with a darker inner panel.
    arch.color.setFill()
    NSBezierPath(rect: NSRect(origin: .zero, size: size)).fill()

    let panel = NSRect(x: 48, y: 48, width: size.width - 96, height: size.height - 96)
    NSColor(calibratedWhite: 0.08, alpha: 0.55).setFill()
    NSBezierPath(roundedRect: panel, xRadius: 24, yRadius: 24).fill()

    // Simple "character token" silhouette: head circle + shoulders arc.
    let cx = size.width / 2
    NSColor(calibratedWhite: 0.95, alpha: 0.9).setFill()
    NSBezierPath(ovalIn: NSRect(x: cx - 70, y: size.height * 0.52, width: 140, height: 140)).fill()
    let shoulders = NSBezierPath()
    shoulders.move(to: NSPoint(x: cx - 150, y: size.height * 0.30))
    shoulders.curve(to: NSPoint(x: cx + 150, y: size.height * 0.30),
                    controlPoint1: NSPoint(x: cx - 150, y: size.height * 0.62),
                    controlPoint2: NSPoint(x: cx + 150, y: size.height * 0.62))
    shoulders.close()
    shoulders.fill()

    // Labels.
    let white = NSColor(calibratedWhite: 0.96, alpha: 1)
    let dim = NSColor(calibratedWhite: 0.75, alpha: 1)
    text(arch.name, font: .boldSystemFont(ofSize: 84), color: white, centerX: cx, y: 92)
    text(arch.species, font: .systemFont(ofSize: 40, weight: .medium), color: dim, centerX: cx, y: 190)
    text(gender.uppercased(), font: .boldSystemFont(ofSize: 56), color: arch.color, centerX: cx, y: 300)
    text("PLACEHOLDER PORTRAIT", font: .monospacedSystemFont(ofSize: 22, weight: .regular), color: dim, centerX: cx, y: 40)
    text("\(arch.id)_\(gender.lowercased())", font: .monospacedSystemFont(ofSize: 26, weight: .regular), color: dim, centerX: cx, y: size.height - 120)

    image.unlockFocus()
    return image
}

func makeItemIcon(id: String, name: String, color: NSColor, glyph: String) -> NSImage {
    let size: CGFloat = 256
    let image = NSImage(size: NSSize(width: size, height: size))
    image.lockFocus()

    color.setFill()
    NSBezierPath(roundedRect: NSRect(x: 8, y: 8, width: size - 16, height: size - 16), xRadius: 28, yRadius: 28).fill()

    // Inner darker panel.
    NSColor(calibratedWhite: 0.1, alpha: 0.45).setFill()
    NSBezierPath(roundedRect: NSRect(x: 40, y: 40, width: size - 80, height: size - 80), xRadius: 20, yRadius: 20).fill()

    // Glyph shape in light colour.
    let fg = NSColor(calibratedWhite: 0.95, alpha: 1)
    fg.setFill()
    let cx = size / 2
    switch glyph {
    case "potion":
        // flask: circle + neck
        NSBezierPath(ovalIn: NSRect(x: cx - 45, y: 60, width: 90, height: 90)).fill()
        NSBezierPath(rect: NSRect(x: cx - 15, y: 140, width: 30, height: 60)).fill()
    case "crystal":
        let d = NSBezierPath()
        d.move(to: NSPoint(x: cx, y: 200))
        d.line(to: NSPoint(x: cx + 55, y: 130))
        d.line(to: NSPoint(x: cx, y: 60))
        d.line(to: NSPoint(x: cx - 55, y: 130))
        d.close()
        d.fill()
    case "weapon":
        let w = NSBezierPath()
        w.move(to: NSPoint(x: 70, y: 190))
        w.line(to: NSPoint(x: 190, y: 70))
        w.line(to: NSPoint(x: 205, y: 85))
        w.line(to: NSPoint(x: 85, y: 205))
        w.close()
        w.fill()
        NSBezierPath(rect: NSRect(x: 55, y: 55, width: 40, height: 40)).fill()
    case "armour":
        NSBezierPath(roundedRect: NSRect(x: cx - 50, y: 80, width: 100, height: 110), xRadius: 18, yRadius: 18).fill()
    default:
        text(glyph, font: .boldSystemFont(ofSize: 90), color: fg, centerX: cx, y: 110)
    }

    text(name, font: .systemFont(ofSize: 26, weight: .semibold), color: NSColor(calibratedWhite: 0.92, alpha: 1), centerX: cx, y: 18)
    image.unlockFocus()
    return image
}

let mode = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "all"

if mode == "portraits" || mode == "all" {
    let pdir = outDir.appendingPathComponent("Portraits")
    try? FileManager.default.createDirectory(at: pdir, withIntermediateDirectories: true)
    for arch in archetypes {
        for gender in ["male", "female"] {
            savePNG(makePortrait(arch, gender: gender), to: pdir.appendingPathComponent("\(arch.id)_\(gender).png"))
        }
    }
}

if mode == "items" || mode == "all" {
    let idir = outDir.appendingPathComponent("Items")
    try? FileManager.default.createDirectory(at: idir, withIntermediateDirectories: true)
    let items: [(String, String, NSColor, String)] = [
        ("apprentice_focus_staff", "APPRENTICE STAFF", NSColor(calibratedRed: 0.55, green: 0.36, blue: 0.95, alpha: 1), "weapon"),
        ("reinforced_club", "REINFORCED CLUB", NSColor(calibratedRed: 0.55, green: 0.40, blue: 0.28, alpha: 1), "weapon"),
        ("training_composite_bow", "TRAINING BOW", NSColor(calibratedRed: 0.35, green: 0.65, blue: 0.45, alpha: 1), "weapon"),
        ("service_blade", "SERVICE BLADE", NSColor(calibratedRed: 0.45, green: 0.55, blue: 0.75, alpha: 1), "weapon"),
        ("medics_focus_wand", "MEDIC WAND", NSColor(calibratedRed: 0.90, green: 0.70, blue: 0.40, alpha: 1), "weapon"),
        ("basic_protective_jacket", "JACKET", NSColor(calibratedRed: 0.50, green: 0.38, blue: 0.28, alpha: 1), "armour"),
        ("health_potion", "HEALTH POTION", NSColor(calibratedRed: 0.80, green: 0.25, blue: 0.30, alpha: 1), "potion"),
        ("mana_potion", "MANA POTION", NSColor(calibratedRed: 0.25, green: 0.45, blue: 0.85, alpha: 1), "potion"),
        ("save_crystal", "SAVE CRYSTAL", NSColor(calibratedRed: 0.65, green: 0.40, blue: 0.90, alpha: 1), "crystal"),
    ]
    for (id, name, color, glyph) in items {
        savePNG(makeItemIcon(id: id, name: name, color: color, glyph: glyph), to: idir.appendingPathComponent("\(id).png"))
    }
}

// Adapted from tomkpunkt/ParkManager UI/webpack.config.js (GPL-3.0).
const path=require('path');
const webpack=require('webpack');
const MiniCssExtractPlugin=require('mini-css-extract-plugin');
const mod=require('./mod.json');
module.exports={
 entry:{[mod.id]:'./src/index.tsx'},
 externalsType:'window',externals:{react:'React','react-dom':'ReactDOM','cs2/api':'cs2/api','cs2/modding':'cs2/modding','cs2/l10n':'cs2/l10n','cs2/ui':'cs2/ui'},
 resolve:{extensions:['.tsx','.ts','.js']},
 module:{rules:[{test:/\.tsx?$/,exclude:/node_modules/,use:'ts-loader'},{test:/\.scss$/,use:[MiniCssExtractPlugin.loader,{loader:'css-loader',options:{modules:{auto:true,localIdentName:'cs2as_[local]_[hash:base64:5]'}}},'sass-loader']}]},
 output:{filename:'[name].js',path:path.resolve(__dirname,'build'),clean:true,library:{type:'module'},publicPath:'coui://ui-mods/'},
 experiments:{outputModule:true},optimization:{minimize:false},
 plugins:[new MiniCssExtractPlugin(),new webpack.BannerPlugin(`Cities: Skylines II UI Module\nId: ${mod.id}\nAuthor: ${mod.author}\nVersion: ${mod.version}\nDependencies: ${mod.dependencies.join(',')}`)]
};
